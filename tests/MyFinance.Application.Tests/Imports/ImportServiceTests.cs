using Microsoft.Extensions.DependencyInjection;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Imports;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Tests.Imports;

public sealed class ImportServiceTests : ApplicationTestBase
{
    private static string Ofx(string acctId, params (string Date, string Amount, string FitId, string Memo)[] transactions) => $"""
        OFXHEADER:100
        DATA:OFXSGML

        <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>
        <BANKACCTFROM><ACCTID>{acctId}</ACCTID></BANKACCTFROM>
        <BANKTRANLIST>
        {string.Concat(transactions.Select(t => $"<STMTTRN><DTPOSTED>{t.Date}<TRNAMT>{t.Amount}<FITID>{t.FitId}<MEMO>{t.Memo}</STMTTRN>\n"))}
        </BANKTRANLIST>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    private static readonly string SeptemberOfx = Ofx("99999999-9",
        ("20260929", "-120.50", "F1", "Supermercado"),
        ("20260928", "-35.90", "F2", "Uber"),
        ("20260927", "-49.90", "F3", "Netflix"));

    private ImportService Service => Host.Get<ImportService>();

    private async Task<int> CountTransactionsAsync() =>
        (await Host.Get<TransactionService>().SearchAsync(new TransactionSearch(), Ct)).TotalCount;

    private async Task<(ImportPreview Preview, ImportOutcome Outcome)> ImportAsync(
        string fileName, string content, TransactionOwner owner, bool? invert = null)
    {
        var service = Service;
        var analysis = await service.AnalyzeAsync(fileName, Text(content), Ct);
        var preview = await service.PreviewAsync(analysis, owner, invert, Ct);
        return (preview, await service.ConfirmAsync(preview, Ct));
    }

    [Fact]
    public async Task Importar_OFX_novo_mostra_previa_e_so_grava_apos_confirmar()
    {
        var accountId = await CreateAccountAsync();
        var owner = TransactionOwner.ForAccount(accountId);
        var service = Service;

        var analysis = await service.AnalyzeAsync(@"C:\Downloads\extrato.ofx", Text(SeptemberOfx), Ct);

        analysis.FileName.Should().Be("extrato.ofx");
        analysis.FileType.Should().Be(ImportFileType.Ofx);
        analysis.TotalFound.Should().Be(3);
        analysis.PreviousImport.Should().BeNull();
        analysis.SuggestedOwner.Should().Be(owner, "o ACCTID do arquivo coincide com o número da conta");

        var preview = await service.PreviewAsync(analysis, owner, null, Ct);

        preview.OwnerName.Should().Be("Nubank");
        preview.Summary.Should().Be(new ImportSummary(Total: 3, New: 3, Duplicates: 0, Errors: 0));
        preview.Rows.Select(r => (r.Date, r.Description, r.Amount, r.Status)).Should().Equal(
            (Day(29), "Supermercado", -120.50m, ImportTransactionStatus.New),
            (Day(28), "Uber", -35.90m, ImportTransactionStatus.New),
            (Day(27), "Netflix", -49.90m, ImportTransactionStatus.New));
        (await CountTransactionsAsync()).Should().Be(0, "nada é gravado antes da confirmação");

        var outcome = await service.ConfirmAsync(preview, Ct);

        outcome.Summary.Should().Be(new ImportSummary(3, 3, 0, 0));
        (await CountTransactionsAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Reimportar_o_mesmo_arquivo_avisa_e_marca_todas_como_duplicadas()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        await ImportAsync("extrato.ofx", SeptemberOfx, owner);

        var analysis = await Service.AnalyzeAsync("extrato-copia.ofx", Text(SeptemberOfx), Ct);

        analysis.PreviousImport.Should().BeEquivalentTo(new PreviousImportInfo(Host.Clock.Now.UtcDateTime, 3, "extrato.ofx"));

        var preview = await Service.PreviewAsync(analysis, owner, null, Ct);
        preview.Summary.Should().Be(new ImportSummary(3, 0, 3, 0));
        preview.Rows.Should().AllSatisfy(r => r.DuplicateReason.Should().Be(DuplicateReason.ExternalId));

        var outcome = await Service.ConfirmAsync(preview, Ct);
        outcome.Summary.New.Should().Be(0);
        (await CountTransactionsAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Periodo_sobreposto_importa_somente_as_novas()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        await ImportAsync("setembro.ofx", SeptemberOfx, owner);

        var overlapping = Ofx("99999999-9",
            ("20260929", "-120.50", "F1", "Supermercado"),
            ("20260930", "-10.00", "F4", "Padaria"),
            ("20261001", "-15.00", "F5", "Farmácia"));

        var (preview, outcome) = await ImportAsync("parcial.ofx", overlapping, owner);

        preview.Rows.Select(r => r.Status).Should().Equal(
            ImportTransactionStatus.Duplicate, ImportTransactionStatus.New, ImportTransactionStatus.New);
        outcome.Summary.Should().Be(new ImportSummary(3, 2, 1, 0));
        (await CountTransactionsAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Mesmo_extrato_em_outro_formato_e_detectado_pela_combinacao_de_dados()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        await ImportAsync("extrato.ofx", SeptemberOfx, owner);

        // A coluna de saldo muda o hash; só a combinação conta + data + valor + descrição identifica a duplicidade.
        // (Sem saldo, o hash seria igual ao do OFX e a estratégia ImportHash já detectaria.)
        const string csv = """
            Data;Descrição;Valor;Saldo
            29/09/2026;SUPERMERCADO;-120,50;1.000,00
            28/09/2026;Uber;-35,90;1.120,50
            26/09/2026;Cinema;-40,00;1.156,40
            """;

        var (preview, _) = await ImportAsync("extrato.csv", csv, owner);

        preview.Rows.Select(r => (r.Status, r.DuplicateReason)).Should().Equal(
            (ImportTransactionStatus.Duplicate, DuplicateReason.SameData),
            (ImportTransactionStatus.Duplicate, DuplicateReason.SameData),
            (ImportTransactionStatus.New, DuplicateReason.None));
    }

    [Fact]
    public async Task Duplicidade_e_verificada_somente_na_mesma_conta()
    {
        var first = TransactionOwner.ForAccount(await CreateAccountAsync("Conta 1", number: "111"));
        var second = TransactionOwner.ForAccount(await CreateAccountAsync("Conta 2", number: "222"));
        await ImportAsync("extrato.ofx", SeptemberOfx, first);

        var (preview, _) = await ImportAsync("extrato.ofx", SeptemberOfx, second);

        preview.Summary.New.Should().Be(3);
    }

    [Fact]
    public async Task Registros_invalidos_sao_exibidos_e_gravados_como_historico()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        const string csv = """
            Data;Histórico;Valor
            01/09/2026;Saldo anterior;0,00
            02/09/2026;Tarifa;-1234,567
            03/09/2026;Salário;7.250,35
            99/09/2026;Data ruim;-1,00
            """;

        var (preview, outcome) = await ImportAsync("extrato.csv", csv, owner);

        preview.Rows.Select(r => (r.Status, r.Message)).Should().Equal(
            (ImportTransactionStatus.Invalid, "O valor do lançamento não pode ser zero."),
            (ImportTransactionStatus.Invalid, "O valor do lançamento deve ter no máximo 2 casas decimais."),
            (ImportTransactionStatus.New, null),
            (ImportTransactionStatus.Invalid, "Registro 5: Data inválida: \"99/09/2026\"."));
        outcome.Summary.Should().Be(new ImportSummary(Total: 4, New: 1, Duplicates: 0, Errors: 3));
        (await CountTransactionsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Fatura_CSV_com_compras_positivas_sugere_e_aplica_inversao_de_sinal()
    {
        var owner = TransactionOwner.ForCreditCard(await CreateCardAsync());
        const string csv = """
            date,title,amount
            2026-09-27,Netflix.com,49.90
            2026-09-26,Ifood,89.00
            2026-09-20,Pagamento recebido,-1500.00
            """;
        var analysis = await Service.AnalyzeAsync("fatura.csv", Text(csv), Ct);

        var automatic = await Service.PreviewAsync(analysis, owner, invertAmounts: null, Ct);
        var kept = await Service.PreviewAsync(analysis, owner, invertAmounts: false, Ct);

        automatic.InversionSuggested.Should().BeTrue();
        automatic.AmountsInverted.Should().BeTrue();
        automatic.Rows.Select(r => r.Amount).Should().Equal(-49.90m, -89.00m, 1500.00m);
        kept.AmountsInverted.Should().BeFalse();
        kept.Rows.Select(r => r.Amount).Should().Equal(49.90m, 89.00m, -1500.00m);
    }

    [Fact]
    public async Task Conta_bancaria_nunca_sugere_inversao()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        var analysis = await Service.AnalyzeAsync("extrato.csv", Text("Data,Valor,Descrição\n01/09/2026,5000.00,Salário"), Ct);

        var preview = await Service.PreviewAsync(analysis, owner, null, Ct);

        preview.InversionSuggested.Should().BeFalse();
        preview.Rows.Single().Amount.Should().Be(5000m);
    }

    [Fact]
    public async Task OFX_de_cartao_sugere_o_cartao_pelos_ultimos_digitos()
    {
        await CreateAccountAsync();
        var cardId = await CreateCardAsync(lastFour: "4321");
        const string ofx = """
            <OFX><CREDITCARDMSGSRSV1><CCSTMTTRNRS><CCSTMTRS>
            <CCACCTFROM><ACCTID>5555444433334321</ACCTID></CCACCTFROM>
            <BANKTRANLIST><STMTTRN><DTPOSTED>20260910<TRNAMT>-10<FITID>1<NAME>Loja</STMTTRN></BANKTRANLIST>
            </CCSTMTRS></CCSTMTTRNRS></CREDITCARDMSGSRSV1></OFX>
            """;

        var analysis = await Service.AnalyzeAsync("cartao.ofx", Text(ofx), Ct);

        analysis.StatementKind.Should().Be(StatementKind.CreditCard);
        analysis.SuggestedOwner.Should().Be(TransactionOwner.ForCreditCard(cardId));
    }

    [Theory]
    [InlineData("extrato.pdf", "O formato do arquivo não é reconhecido.*")]
    [InlineData("extrato.ofx", "O arquivo está vazio.")]
    public async Task Arquivo_invalido_gera_erro_de_importacao_amigavel(string fileName, string message)
    {
        var act = () => Service.AnalyzeAsync(fileName, Text(string.Empty), Ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage(message);
    }

    [Fact]
    public async Task Destino_inativo_ou_inexistente_gera_erro_de_validacao()
    {
        var accountId = await CreateAccountAsync();
        await Host.Get<MyFinance.Application.Accounts.AccountService>().DeactivateAsync(accountId, Ct);
        var analysis = await Service.AnalyzeAsync("extrato.ofx", Text(SeptemberOfx), Ct);

        var inactive = () => Service.PreviewAsync(analysis, TransactionOwner.ForAccount(accountId), null, Ct);
        var missing = () => Service.PreviewAsync(analysis, TransactionOwner.ForCreditCard(Guid.CreateVersion7()), null, Ct);

        await inactive.Should().ThrowAsync<ValidationException>().WithMessage("Selecione uma conta ativa.");
        await missing.Should().ThrowAsync<ValidationException>().WithMessage("Selecione um cartão ativo.");
    }

    [Fact]
    public async Task Confirmar_duas_vezes_e_bloqueado()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        var (preview, _) = await ImportAsync("extrato.ofx", SeptemberOfx, owner);

        var act = () => Service.ConfirmAsync(preview, Ct);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*já foi confirmada*");
    }

    [Fact]
    public async Task Arquivo_sem_transacoes_nao_pode_ser_confirmado()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        var analysis = await Service.AnalyzeAsync("vazio.csv", Text("Data,Valor,Descrição\n"), Ct);
        var preview = await Service.PreviewAsync(analysis, owner, null, Ct);

        var act = () => Service.ConfirmAsync(preview, Ct);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*não contém transações*");
    }

    [Fact]
    public async Task Duplicidades_sao_recalculadas_na_confirmacao()
    {
        // Duas prévias do mesmo arquivo abertas ao mesmo tempo: a segunda confirmação não pode duplicar.
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        var analysis = await Service.AnalyzeAsync("extrato.ofx", Text(SeptemberOfx), Ct);
        var first = await Service.PreviewAsync(analysis, owner, null, Ct);
        var second = await Service.PreviewAsync(analysis, owner, null, Ct);

        await Service.ConfirmAsync(first, Ct);
        var outcome = await Service.ConfirmAsync(second, Ct);

        outcome.Summary.Should().Be(new ImportSummary(3, 0, 3, 0));
        (await CountTransactionsAsync()).Should().Be(3);
    }
}

public sealed class ImportServiceCategorizationTests : ApplicationTestBase
{
    private sealed class DeliveryRule(Func<Task<Guid>> categoryId) : ICategorizationService
    {
        public async Task<Guid?> SuggestCategoryAsync(CategorizationInput input, CancellationToken cancellationToken) =>
            input.Description.Contains("IFOOD", StringComparison.OrdinalIgnoreCase) ? await categoryId() : null;
    }

    private protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<ICategorizationService>(new DeliveryRule(() => CategoryIdAsync("Alimentação > Delivery")));

    [Fact]
    public async Task Servico_de_categorizacao_e_aplicado_aos_lancamentos_novos()
    {
        var owner = TransactionOwner.ForAccount(await CreateAccountAsync());
        var service = Host.Get<ImportService>();
        var analysis = await service.AnalyzeAsync("extrato.csv", Text("Data,Valor,Descrição\n01/09/2026,-50.00,IFOOD *PIZZA\n02/09/2026,-10.00,Padaria"), Ct);

        await service.ConfirmAsync(await service.PreviewAsync(analysis, owner, null, Ct), Ct);

        var items = (await Host.Get<TransactionService>().SearchAsync(new TransactionSearch(), Ct)).Items;
        items.Single(i => i.Description == "IFOOD *PIZZA").CategoryName.Should().Be("Alimentação > Delivery");
        items.Single(i => i.Description == "Padaria").CategoryId.Should().BeNull();
    }
}