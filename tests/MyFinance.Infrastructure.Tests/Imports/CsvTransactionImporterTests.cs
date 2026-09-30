using System.Text;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Enums;
using MyFinance.Infrastructure.Imports.CSV;

using static MyFinance.Infrastructure.Tests.Imports.ImportTestHelpers;

namespace MyFinance.Infrastructure.Tests.Imports;

public sealed class CsvTransactionImporterTests
{
    private readonly CsvTransactionImporter _importer = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("extrato.csv", true)]
    [InlineData("Extrato.CSV", true)]
    [InlineData("extrato.ofx", false)]
    public void CanHandle_pela_extensao(string fileName, bool expected) =>
        _importer.CanHandle(fileName).Should().Be(expected);

    [Fact]
    public async Task CSV_valido_de_conta_no_formato_Nubank()
    {
        await using var stream = File("nubank-conta.csv");

        var result = await _importer.ImportAsync(stream, _ct);

        result.FileType.Should().Be(ImportFileType.Csv);
        result.Errors.Should().BeEmpty();
        result.Transactions.Should().HaveCount(3);
        result.Transactions[0].Should().BeEquivalentTo(new
        {
            Date = Day(29),
            Amount = -120.50m,
            Description = "Compra no débito - Supermercado",
            ExternalId = "6512a1f0-0001-4c1e-9d6b-000000000001",
            RawData = "29/09/2026,-120.50,6512a1f0-0001-4c1e-9d6b-000000000001,Compra no débito - Supermercado",
        });
        result.Transactions[2].Description.Should().Be("Transferência recebida pelo Pix - EMPRESA EXEMPLO LTDA, CNPJ 00.000.000/0001-00");
    }

    [Fact]
    public async Task CSV_de_cartao_mantem_o_sinal_do_arquivo()
    {
        await using var stream = File("nubank-cartao.csv");

        var result = await _importer.ImportAsync(stream, _ct);

        result.Transactions.Select(t => (t.Date, t.Amount, t.Description)).Should().Equal(
            (Day(27), 49.90m, "Netflix.com"),
            (Day(26), 89.00m, "Ifood *Restaurante"),
            (Day(20), -1500.00m, "Pagamento recebido"));
        result.Transactions.Should().AllSatisfy(t => t.ExternalId.Should().BeNull());
    }

    [Fact]
    public async Task CSV_com_ponto_e_virgula_valores_brasileiros_e_saldo()
    {
        await using var stream = File("banco-ponto-e-virgula.csv");

        var result = await _importer.ImportAsync(stream, _ct);

        result.Errors.Should().BeEmpty();
        result.Transactions.Select(t => (t.Description, t.Amount, t.Balance)).Should().Equal(
            ("Saldo anterior", 0m, 2000m),
            ("PIX ENVIADO; ALUGUEL", -1500m, 500m),
            ("SALARIO", 7250.35m, 7750.35m),
            ("TARIFA PACOTE", -39.90m, 7710.45m));
    }

    [Fact]
    public async Task CSV_com_BOM_aspas_escapadas_e_quebra_de_linha_no_campo()
    {
        var csv = "﻿data;descricao;valor\r\n01/09/2026;\"Loja \"\"Central\"\"\r\nfilial 2\";-10,00\r\n";

        var result = await _importer.ImportAsync(Utf8(csv), _ct);

        result.Transactions.Should().ContainSingle().Which.Description.Should().Be("Loja \"Central\"\r\nfilial 2");
    }

    [Fact]
    public async Task CSV_em_Windows_1252()
    {
        var result = await _importer.ImportAsync(Windows1252("Data;Descrição;Valor\n01/09/2026;Açougue;-10,00\n"), _ct);

        result.Transactions.Single().Description.Should().Be("Açougue");
    }

    [Fact]
    public async Task Arquivo_vazio_gera_ImportException()
    {
        var act = () => _importer.ImportAsync(new MemoryStream(), _ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage("*vazio*");
    }

    [Fact]
    public async Task CSV_sem_cabecalho_reconhecivel_gera_ImportException()
    {
        var act = () => _importer.ImportAsync(Utf8("01/09/2026,-10.00,Mercado\n02/09/2026,-5.00,Padaria"), _ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage("*identificar as colunas*");
    }

    [Fact]
    public async Task CSV_somente_com_cabecalho_retorna_resultado_vazio()
    {
        var result = await _importer.ImportAsync(Utf8("Data,Valor,Descrição\n\n"), _ct);

        result.TotalFound.Should().Be(0);
    }

    [Fact]
    public async Task Linhas_invalidas_viram_erros_com_numero_da_linha()
    {
        const string csv = """
            Data,Valor,Descrição
            01/09/2026,-10.00,Válida

            31/02/2026,-1.00,Data inexistente
            2026/13/01,-1.00,Data inválida
            03/09/2026,dez reais,Valor inválido
            04/09/2026,-1.00,
            05/09/2026,-1.00
            06/09/2026,"1.234,56",Valor com milhar
            """;

        var result = await _importer.ImportAsync(Utf8(csv), _ct);

        result.Transactions.Select(t => t.Amount).Should().Equal(-10m, 1234.56m);
        result.Errors.Select(e => (e.Position, e.Message)).Should().Equal(
            (4, "Data inválida: \"31/02/2026\"."),
            (5, "Data inválida: \"2026/13/01\"."),
            (6, "Valor inválido: \"dez reais\"."),
            (7, "Transação sem descrição."),
            (8, "Linha com 2 coluna(s); esperado ao menos 3."));
        result.Errors[0].RawData.Should().Be("31/02/2026,-1.00,Data inexistente");
    }

    [Fact]
    public async Task Linha_com_colunas_a_mais_e_rejeitada_em_vez_de_deslocar_valores()
    {
        // Vírgula decimal sem aspas em CSV separado por vírgula: "-10,50" vira duas colunas.
        var result = await _importer.ImportAsync(Utf8("Data,Valor,Descricao\n01/01/2026,-10,50,Padaria\n02/01/2026,-5.00,Café,,\n"), _ct);

        result.Errors.Should().ContainSingle().Which.Message.Should().Be("Linha com 4 coluna(s); o cabeçalho tem 3.");
        result.Transactions.Should().ContainSingle("colunas extras vazias no fim da linha são toleradas")
            .Which.Description.Should().Be("Café");
    }

    [Fact]
    public async Task Mensagem_de_erro_nao_carrega_o_campo_inteiro()
    {
        var huge = new string('x', 5000);

        var result = await _importer.ImportAsync(Utf8($"Data,Valor,Descricao\n01/01/2026,{huge},Loja\n"), _ct);

        result.Errors.Single().Message.Length.Should().BeLessThan(120);
    }

    [Fact]
    public async Task Arquivo_acima_do_limite_gera_ImportException()
    {
        var big = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 20 * 1024 * 1024 + 1)));

        var act = () => _importer.ImportAsync(big, _ct);

        await act.Should().ThrowAsync<ImportException>().WithMessage("*grande demais*");
    }
}