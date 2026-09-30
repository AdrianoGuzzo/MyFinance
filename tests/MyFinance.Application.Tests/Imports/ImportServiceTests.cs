using Microsoft.Extensions.DependencyInjection;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Imports;
using MyFinance.Application.Invoices;
using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Tests.Imports;

public sealed class ImportServiceTests : ApplicationTestBase
{
    internal static string Ofx(string acctId, params (string Date, string Amount, string FitId, string Memo)[] transactions) => $"""
        OFXHEADER:100
        DATA:OFXSGML

        <OFX><CREDITCARDMSGSRSV1><CCSTMTTRNRS><CCSTMTRS>
        <CCACCTFROM><ACCTID>{acctId}</ACCTID></CCACCTFROM>
        <BANKTRANLIST>
        {string.Concat(transactions.Select(t => $"<STMTTRN><DTPOSTED>{t.Date}<TRNAMT>{t.Amount}<FITID>{t.FitId}<MEMO>{t.Memo}</STMTTRN>\n"))}
        </BANKTRANLIST>
        </CCSTMTRS></CCSTMTTRNRS></CREDITCARDMSGSRSV1></OFX>
        """;

    private static readonly string SeptemberOfx = Ofx("5555444433331234",
        ("20260929", "-120.50", "F1", "Supermercado"),
        ("20260928", "-35.90", "F2", "Uber"),
        ("20260927", "-49.90", "F3", "Netflix"));

    private ImportService Service => Host.Get<ImportService>();

    private async Task<int> CountTransactionsAsync() =>
        (await Host.Get<TransactionService>().SearchAsync(new TransactionSearch(), Ct)).TotalCount;

    private async Task<(ImportPreview Preview, ImportOutcome Outcome)> ImportAsync(
        string fileName, string content, Guid cardId, bool? invert = null)
    {
        var service = Service;
        var analysis = await service.AnalyzeAsync(fileName, Text(content), Ct);
        var preview = await service.PreviewAsync(analysis, cardId, invert, Ct);
        return (preview, await service.ConfirmAsync(preview, Ct));
    }

    [Fact]
    public async Task Importar_OFX_novo_mostra_previa_e_so_grava_apos_confirmar()
    {
        var cardId = await CreateCardAsync(lastFour: "1234", closingDay: 3, dueDay: 10);
        var service = Service;

        var analysis = await service.AnalyzeAsync(@"C:\Downloads\fatura.ofx", Text(SeptemberOfx), Ct);

        analysis.FileName.Should().Be("fatura.ofx");
        analysis.FileType.Should().Be(ImportFileType.Ofx);
        analysis.StatementKind.Should().Be(StatementKind.CreditCard);
        analysis.TotalFound.Should().Be(3);
        analysis.PreviousImport.Should().BeNull();
        analysis.SuggestedCreditCardId.Should().Be(cardId, "os 4 últimos dígitos do ACCTID coincidem com os do cartão");

        var preview = await service.PreviewAsync(analysis, cardId, null, Ct);

        preview.CreditCardName.Should().Be("Nubank Visa");
        preview.Summary.Should().Be(new ImportSummary(Total: 3, New: 3, Duplicates: 0, Errors: 0));
        preview.Rows.Select(r => (r.Date, r.Description, r.Amount, r.Status, r.InvoiceMonth, r.Kind)).Should().Equal(
            (Day(29), "Supermercado", -120.50m, ImportTransactionStatus.New, Month(10), TransactionKind.Purchase),
            (Day(28), "Uber", -35.90m, ImportTransactionStatus.New, Month(10), TransactionKind.Purchase),
            (Day(27), "Netflix", -49.90m, ImportTransactionStatus.New, Month(10), TransactionKind.Purchase));
        (await CountTransactionsAsync()).Should().Be(0, "nada é gravado antes da confirmação");

        var outcome = await service.ConfirmAsync(preview, Ct);

        outcome.Summary.Should().Be(new ImportSummary(3, 3, 0, 0));
        (await CountTransactionsAsync()).Should().Be(3);
        var invoice = (await Host.Get<InvoiceService>().ListAsync(cardId, Ct)).Single();
        invoice.ReferenceMonth.Should().Be(Month(10));
        invoice.Total.Should().Be(206.30m);
        invoice.TransactionCount.Should().Be(3);
    }

    [Fact]
    public async Task Reimportar_a_mesma_fatura_avisa_marca_todas_como_duplicadas_e_nao_cria_outra_fatura()
    {
        var cardId = await CreateCardAsync();
        await ImportAsync("fatura.ofx", SeptemberOfx, cardId);

        var analysis = await Service.AnalyzeAsync("fatura-copia.ofx", Text(SeptemberOfx), Ct);

        analysis.PreviousImport.Should().BeEquivalentTo(new PreviousImportInfo(Host.Clock.Now.UtcDateTime, 3, "fatura.ofx"));

        var preview = await Service.PreviewAsync(analysis, cardId, null, Ct);
        preview.Summary.Should().Be(new ImportSummary(3, 0, 3, 0));
        preview.Rows.Should().AllSatisfy(r => r.DuplicateReason.Should().Be(DuplicateReason.ExternalId));

        var outcome = await Service.ConfirmAsync(preview, Ct);
        outcome.Summary.New.Should().Be(0);
        (await CountTransactionsAsync()).Should().Be(3);
        (await Host.Get<InvoiceService>().ListAsync(cardId, Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task Periodo_sobreposto_importa_somente_as_novas_e_reutiliza_a_fatura_existente()
    {
        var cardId = await CreateCardAsync(closingDay: 3, dueDay: 10);
        await ImportAsync("setembro.ofx", SeptemberOfx, cardId);

        var overlapping = Ofx("1234",
            ("20260929", "-120.50", "F1", "Supermercado"),
            ("20261002", "-10.00", "F4", "Padaria"),
            ("20261003", "-15.00", "F5", "Farmácia"));

        var (preview, outcome) = await ImportAsync("parcial.ofx", overlapping, cardId);

        preview.Rows.Select(r => (r.Status, r.InvoiceMonth)).Should().Equal(
            (ImportTransactionStatus.Duplicate, Month(10)),
            (ImportTransactionStatus.New, Month(10)),
            (ImportTransactionStatus.New, Month(11)));
        outcome.Summary.Should().Be(new ImportSummary(3, 2, 1, 0));
        (await CountTransactionsAsync()).Should().Be(5);
        (await Host.Get<InvoiceService>().ListAsync(cardId, Ct)).Select(i => (i.ReferenceMonth, i.Total))
            .Should().Equal((Month(11), 15.00m), (Month(10), 216.30m));
    }

    [Fact]
    public async Task Mesma_fatura_em_outro_formato_e_detectada_pela_combinacao_de_dados()
    {
        var cardId = await CreateCardAsync();
        await ImportAsync("fatura.ofx", SeptemberOfx, cardId);

        // A coluna de saldo muda o hash; só a combinação cartão + data + valor + descrição identifica a duplicidade.
        const string csv = """
            Data;Descrição;Valor;Saldo
            29/09/2026;SUPERMERCADO;-120,50;1.000,00
            28/09/2026;Uber;-35,90;1.120,50
            26/09/2026;Cinema;-40,00;1.156,40
            """;

        var (preview, _) = await ImportAsync("fatura.csv", csv, cardId, invert: false);

        preview.Rows.Select(r => (r.Status, r.DuplicateReason)).Should().Equal(
            (ImportTransactionStatus.Duplicate, DuplicateReason.SameData),
            (ImportTransactionStatus.Duplicate, DuplicateReason.SameData),
            (ImportTransactionStatus.New, DuplicateReason.None));
    }

    [Fact]
    public async Task Duplicidade_e_verificada_somente_no_mesmo_cartao()
    {
        var first = await CreateCardAsync("Cartão 1", "1111");
        var second = await CreateCardAsync("Cartão 2", "2222");
        await ImportAsync("fatura.ofx", SeptemberOfx, first);

        var (preview, _) = await ImportAsync("fatura.ofx", SeptemberOfx, second);

        preview.Summary.New.Should().Be(3);
    }

    [Fact]
    public async Task Registros_invalidos_sao_exibidos_e_gravados_como_historico()
    {
        var cardId = await CreateCardAsync();
        const string csv = """
            Data;Histórico;Valor
            01/09/2026;Saldo anterior;0,00
            02/09/2026;Tarifa;-1234,567
            03/09/2026;Restaurante;-72,35
            99/09/2026;Data ruim;-1,00
            """;

        var (preview, outcome) = await ImportAsync("fatura.csv", csv, cardId, invert: false);

        preview.Rows.Select(r => (r.Status, r.Message)).Should().Equal(
            (ImportTransactionStatus.Invalid, "O valor do lançamento não pode ser zero."),
            (ImportTransactionStatus.Invalid, "O valor do lançamento deve ter no máximo 2 casas decimais."),
            (ImportTransactionStatus.New, null),
            (ImportTransactionStatus.Invalid, "Registro 5: Data inválida: \"99/09/2026\"."));
        outcome.Summary.Should().Be(new ImportSummary(Total: 4, New: 1, Duplicates: 0, Errors: 3));
        (await CountTransactionsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Fatura_CSV_com_compras_positivas_sugere_inversao_e_identifica_pagamento()
    {
        var cardId = await CreateCardAsync();
        const string csv = """
            date,title,amount
            2026-09-27,Netflix.com,49.90
            2026-09-26,Ifood,89.00
            2026-09-20,Pagamento recebido,-1500.00
            """;
        var analysis = await Service.AnalyzeAsync("fatura.csv", Text(csv), Ct);

        var automatic = await Service.PreviewAsync(analysis, cardId, invertAmounts: null, Ct);
        var kept = await Service.PreviewAsync(analysis, cardId, invertAmounts: false, Ct);

        automatic.InversionSuggested.Should().BeTrue();
        automatic.AmountsInverted.Should().BeTrue();
        automatic.Rows.Select(r => (r.Amount, r.Kind)).Should().Equal(
            (-49.90m, TransactionKind.Purchase), (-89.00m, TransactionKind.Purchase), (1500.00m, TransactionKind.Payment));
        kept.AmountsInverted.Should().BeFalse();
        kept.Rows.Select(r => r.Amount).Should().Equal(49.90m, 89.00m, -1500.00m);

        await Service.ConfirmAsync(automatic, Ct);
        var invoice = (await Host.Get<InvoiceService>().ListAsync(cardId, Ct)).Single();
        invoice.Total.Should().Be(138.90m, "o pagamento não é gasto nem reduz a fatura");
        invoice.Payments.Should().Be(1500m);
    }

    [Fact]
    public async Task Extrato_de_conta_bancaria_e_recusado()
    {
        const string bankOfx = """
            <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>
            <BANKACCTFROM><ACCTID>99999999-9</ACCTID></BANKACCTFROM>
            <BANKTRANLIST><STMTTRN><DTPOSTED>20260910<TRNAMT>-10<FITID>1<NAME>Pix</STMTTRN></BANKTRANLIST>
            </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;

        var act = () => Service.AnalyzeAsync("conta.ofx", Text(bankOfx), Ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage("*extrato de conta bancária*");
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
    public async Task Cartao_inativo_ou_inexistente_gera_erro_de_validacao()
    {
        var cardId = await CreateCardAsync();
        await Host.Get<CreditCardService>().DeactivateAsync(cardId, Ct);
        var analysis = await Service.AnalyzeAsync("fatura.ofx", Text(SeptemberOfx), Ct);

        var inactive = () => Service.PreviewAsync(analysis, cardId, null, Ct);
        var missing = () => Service.PreviewAsync(analysis, Guid.CreateVersion7(), null, Ct);

        analysis.SuggestedCreditCardId.Should().BeNull("cartões inativos não são sugeridos");
        await inactive.Should().ThrowAsync<ValidationException>().WithMessage("Selecione um cartão ativo.");
        await missing.Should().ThrowAsync<ValidationException>().WithMessage("Selecione um cartão ativo.");
    }

    [Fact]
    public async Task Confirmar_duas_vezes_e_bloqueado()
    {
        var cardId = await CreateCardAsync();
        var (preview, _) = await ImportAsync("fatura.ofx", SeptemberOfx, cardId);

        var act = () => Service.ConfirmAsync(preview, Ct);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*já foi confirmada*");
    }

    [Fact]
    public async Task Arquivo_sem_transacoes_nao_pode_ser_confirmado()
    {
        var cardId = await CreateCardAsync();
        var analysis = await Service.AnalyzeAsync("vazio.csv", Text("Data,Valor,Descrição\n"), Ct);
        var preview = await Service.PreviewAsync(analysis, cardId, null, Ct);

        var act = () => Service.ConfirmAsync(preview, Ct);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*não contém transações*");
    }

    [Fact]
    public async Task Duplicidades_sao_recalculadas_na_confirmacao()
    {
        // Duas prévias do mesmo arquivo abertas ao mesmo tempo: a segunda confirmação não pode duplicar.
        var cardId = await CreateCardAsync();
        var analysis = await Service.AnalyzeAsync("fatura.ofx", Text(SeptemberOfx), Ct);
        var first = await Service.PreviewAsync(analysis, cardId, null, Ct);
        var second = await Service.PreviewAsync(analysis, cardId, null, Ct);

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
        var cardId = await CreateCardAsync();
        var service = Host.Get<ImportService>();
        var analysis = await service.AnalyzeAsync("fatura.csv", Text("Data,Valor,Descrição\n01/09/2026,-50.00,IFOOD *PIZZA\n02/09/2026,-10.00,Padaria"), Ct);

        await service.ConfirmAsync(await service.PreviewAsync(analysis, cardId, null, Ct), Ct);

        var items = (await Host.Get<TransactionService>().SearchAsync(new TransactionSearch(), Ct)).Items;
        items.Single(i => i.Description == "IFOOD *PIZZA").CategoryName.Should().Be("Alimentação > Delivery");
        items.Single(i => i.Description == "Padaria").CategoryId.Should().BeNull();
    }
}