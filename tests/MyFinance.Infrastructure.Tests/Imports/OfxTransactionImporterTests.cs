using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Imports;
using MyFinance.Domain.Enums;
using MyFinance.Infrastructure.Imports.OFX;

using static MyFinance.Infrastructure.Tests.Imports.ImportTestHelpers;

namespace MyFinance.Infrastructure.Tests.Imports;

public sealed class OfxTransactionImporterTests
{
    private readonly OfxTransactionImporter _importer = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static string Statement(string transactions) => $"""
        OFXHEADER:100
        DATA:OFXSGML

        <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>
        <BANKACCTFROM><ACCTID>123</ACCTID></BANKACCTFROM>
        <BANKTRANLIST>
        {transactions}
        </BANKTRANLIST>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    [Theory]
    [InlineData("extrato.ofx", true)]
    [InlineData("EXTRATO.OFX", true)]
    [InlineData("extrato.qfx", true)]
    [InlineData("extrato.csv", false)]
    [InlineData("extrato", false)]
    public void CanHandle_pela_extensao(string fileName, bool expected) =>
        _importer.CanHandle(fileName).Should().Be(expected);

    [Fact]
    public async Task OFX_valido_do_Nubank_com_tags_fechadas()
    {
        await using var stream = File("nubank-conta.ofx");

        var result = await _importer.ImportAsync(stream, _ct);

        result.FileType.Should().Be(ImportFileType.Ofx);
        result.StatementKind.Should().Be(StatementKind.BankAccount);
        result.StatementAccountId.Should().Be("99999999-9");
        result.Errors.Should().BeEmpty();
        result.Transactions.Should().HaveCount(3);

        var first = result.Transactions[0];
        first.Date.Should().Be(Day(29));
        first.Amount.Should().Be(-120.50m);
        first.Description.Should().Be("Compra no débito - Supermercado");
        first.ExternalId.Should().Be("6512a1f0-0001-4c1e-9d6b-000000000001");
        first.Balance.Should().BeNull();
        first.RawData.Should().Contain("TRNAMT=-120.50");

        result.Transactions[2].Amount.Should().Be(5000m);
    }

    [Fact]
    public async Task OFX_SGML_de_cartao_sem_tags_de_fechamento()
    {
        await using var stream = File("cartao-sgml.ofx");

        var result = await _importer.ImportAsync(stream, _ct);

        result.StatementKind.Should().Be(StatementKind.CreditCard);
        result.StatementAccountId.Should().Be("1234");
        result.Errors.Should().BeEmpty();
        result.Transactions.Select(t => (t.Date, t.Amount, t.Description, t.ExternalId)).Should().Equal(
            (Day(27), -49.90m, "NETFLIX.COM - Assinatura mensal", "CC-0001"),
            (Day(26), -89.00m, "IFOOD *RESTAURANTE", "CC-0002"),   // <MEMO> vazio sem fechamento
            (Day(20), 1500.00m, "PAGAMENTO RECEBIDO", "CC-0003"));
    }

    [Fact]
    public async Task OFX_2_em_XML()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8" standalone="no"?>
            <?OFX OFXHEADER="200" VERSION="220" SECURITY="NONE" OLDFILEUID="NONE" NEWFILEUID="NONE"?>
            <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>
              <BANKTRANLIST>
                <STMTTRN>
                  <TRNTYPE>DEBIT</TRNTYPE>
                  <DTPOSTED>20260915103000.000[-3:BRT]</DTPOSTED>
                  <TRNAMT>-10.00</TRNAMT>
                  <FITID>X1</FITID>
                  <NAME>P&amp;G Farmácia</NAME>
                </STMTTRN>
              </BANKTRANLIST>
            </STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;

        var result = await _importer.ImportAsync(Utf8(xml), _ct);

        result.Transactions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Date = Day(15), Amount = -10m, Description = "P&G Farmácia", ExternalId = "X1" });
    }

    [Fact]
    public async Task OFX_em_Windows_1252_preserva_acentos()
    {
        var ofx = Statement("<STMTTRN><DTPOSTED>20260910<TRNAMT>-7.50<FITID>1<MEMO>Padaria São João</STMTTRN>");

        var result = await _importer.ImportAsync(Windows1252(ofx), _ct);

        result.Transactions.Single().Description.Should().Be("Padaria São João");
    }

    [Fact]
    public async Task Arquivo_vazio_gera_ImportException()
    {
        var act = () => _importer.ImportAsync(Utf8("  \r\n "), _ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage("*vazio*");
    }

    [Fact]
    public async Task Arquivo_que_nao_e_OFX_gera_ImportException()
    {
        var act = () => _importer.ImportAsync(Utf8("Data,Valor,Descrição\n01/09/2026,-1,x"), _ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage("*não é um OFX válido*");
    }

    [Fact]
    public async Task OFX_sem_extrato_gera_ImportException()
    {
        var act = () => _importer.ImportAsync(Utf8("<OFX><SIGNONMSGSRSV1></SIGNONMSGSRSV1></OFX>"), _ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage("*não contém um extrato*");
    }

    [Fact]
    public async Task Extrato_sem_transacoes_retorna_resultado_vazio()
    {
        var result = await _importer.ImportAsync(Utf8(Statement(string.Empty)), _ct);

        result.TotalFound.Should().Be(0);
    }

    [Fact]
    public async Task Registros_invalidos_viram_erros_sem_interromper_os_validos()
    {
        var ofx = Statement("""
            <STMTTRN><DTPOSTED>20260901<TRNAMT>-1.00<FITID>ok<MEMO>Válida</STMTTRN>
            <STMTTRN><DTPOSTED>20261399<TRNAMT>-2.00<FITID>d<MEMO>Data ruim</STMTTRN>
            <STMTTRN><DTPOSTED>20260903<TRNAMT>abc<FITID>v<MEMO>Valor ruim</STMTTRN>
            <STMTTRN><DTPOSTED>20260904<TRNAMT>-4.00<FITID>s</STMTTRN>
            <STMTTRN><TRNAMT>-5.00<FITID>n<MEMO>Sem data</STMTTRN>
            """);

        var result = await _importer.ImportAsync(Utf8(ofx), _ct);

        result.Transactions.Should().ContainSingle().Which.ExternalId.Should().Be("ok");
        result.Errors.Select(e => (e.Position, e.Message)).Should().Equal(
            (2, "Data inválida: \"20261399\"."),
            (3, "Valor inválido: \"abc\"."),
            (4, "Transação sem descrição."),
            (5, "Data inválida: \"\"."));
        result.Errors.Should().AllSatisfy(e => e.RawData.Should().NotBeNullOrEmpty());
        result.TotalFound.Should().Be(5);
    }

    [Fact]
    public async Task OFX_com_mais_de_um_extrato_e_rejeitado_em_vez_de_misturar_contas()
    {
        const string ofx = """
            <OFX>
            <BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>
            <STMTTRN><DTPOSTED>20260901<TRNAMT>-1<FITID>A<MEMO>Conta</STMTTRN>
            </BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1>
            <CREDITCARDMSGSRSV1><CCSTMTTRNRS><CCSTMTRS><BANKTRANLIST>
            <STMTTRN><DTPOSTED>20260901<TRNAMT>-2<FITID>B<MEMO>Cartão</STMTTRN>
            </BANKTRANLIST></CCSTMTRS></CCSTMTTRNRS></CREDITCARDMSGSRSV1>
            </OFX>
            """;

        var act = () => _importer.ImportAsync(Utf8(ofx), _ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage("*2 extratos*");
    }

    [Fact]
    public async Task Cancelamento_e_respeitado()
    {
        await using var stream = File("nubank-conta.ofx");

        var act = () => _importer.ImportAsync(stream, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}