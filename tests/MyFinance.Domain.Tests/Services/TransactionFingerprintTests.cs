using MyFinance.Domain.Services;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Services;

public sealed class TransactionFingerprintTests
{
    [Theory]
    [InlineData("  Pão   de Açúcar ", "PAO DE ACUCAR")]
    [InlineData("iFood\t*Restaurante", "IFOOD *RESTAURANTE")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeDescription_remove_acentos_espacos_e_caixa(string? input, string expected)
    {
        TransactionFingerprint.NormalizeDescription(input).Should().Be(expected);
    }

    [Fact]
    public void ImportHash_ignora_diferencas_de_formatacao_da_descricao()
    {
        var a = TransactionFingerprint.ComputeImportHash(Day(1), -10m, "Padaria São João", null);
        var b = TransactionFingerprint.ComputeImportHash(Day(1), -10.00m, "PADARIA  SAO JOAO", null);

        a.Should().Be(b);
    }

    [Fact]
    public void ImportHash_muda_quando_o_saldo_e_diferente()
    {
        var a = TransactionFingerprint.ComputeImportHash(Day(1), -10m, "Café", 100m);
        var b = TransactionFingerprint.ComputeImportHash(Day(1), -10m, "Café", 90m);

        a.Should().NotBe(b);
    }

    [Theory]
    [InlineData(2, -10, "Café")]
    [InlineData(1, -11, "Café")]
    [InlineData(1, -10, "Chá")]
    public void ImportHash_muda_quando_data_valor_ou_descricao_mudam(int day, int amount, string description)
    {
        var baseline = TransactionFingerprint.ComputeImportHash(Day(1), -10m, "Café", null);

        TransactionFingerprint.ComputeImportHash(Day(day), amount, description, null).Should().NotBe(baseline);
    }
}