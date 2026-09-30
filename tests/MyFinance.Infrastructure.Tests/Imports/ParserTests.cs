using MyFinance.Infrastructure.Imports.Common;

namespace MyFinance.Infrastructure.Tests.Imports;

public sealed class AmountParserTests
{
    [Theory]
    [InlineData("-120.50", -120.50)]
    [InlineData("-120,50", -120.50)]
    [InlineData("120,5", 120.5)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("-1.234.567,89", -1234567.89)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("R$ -35,90", -35.90)]
    [InlineData("R$ 1.000,00", 1000)]
    [InlineData("(35,90)", -35.90)]
    [InlineData("+10", 10)]
    [InlineData("0", 0)]
    public void Valores_validos(string text, double expected)
    {
        AmountParser.TryParse(text, out var amount).Should().BeTrue();
        amount.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,234,567")]
    [InlineData("1.2.3,4.5")]
    [InlineData("--10")]
    [InlineData("10-")]
    [InlineData("1.500")]
    [InlineData("1,500")]
    [InlineData("-12.340")]
    [InlineData("R$ 2.000")]
    public void Valores_invalidos(string? text) => AmountParser.TryParse(text, out _).Should().BeFalse();

    [Theory]
    [InlineData("0.500", 0.5)]
    [InlineData("1234.567", 1234.567)]
    public void Tres_casas_sem_ambiguidade_de_milhar_sao_decimais(string text, double expected)
    {
        // "0.500" não pode ser milhar (zero à esquerda); "1234.567" não tem agrupamento válido.
        // O domínio rejeita depois valores com mais de 2 casas significativas.
        AmountParser.TryParse(text, out var amount).Should().BeTrue();
        amount.Should().Be((decimal)expected);
    }
}

public sealed class DateParserTests
{
    [Theory]
    [InlineData("29/09/2026")]
    [InlineData("29/9/2026")]
    [InlineData("29/09/26")]
    [InlineData("2026-09-29")]
    [InlineData("29-09-2026")]
    [InlineData("29.09.2026")]
    [InlineData("2026-09-29 10:15:00")]
    [InlineData("2026-09-29T10:15:00")]
    [InlineData(" 29/09/2026 ")]
    public void Datas_csv_validas(string text)
    {
        DateParser.TryParseCsv(text, out var date).Should().BeTrue();
        date.Should().Be(new DateOnly(2026, 9, 29));
    }

    [Theory]
    [InlineData("")]
    [InlineData("31/02/2026")]
    [InlineData("09/29/2026")]
    [InlineData("ontem")]
    public void Datas_csv_invalidas(string text) => DateParser.TryParseCsv(text, out _).Should().BeFalse();

    [Theory]
    [InlineData("20260929")]
    [InlineData("20260929120000")]
    [InlineData("20260929235959.999[-3:BRT]")]
    public void Datas_ofx_usam_a_data_informada_sem_converter_fuso(string text)
    {
        DateParser.TryParseOfx(text, out var date).Should().BeTrue();
        date.Should().Be(new DateOnly(2026, 9, 29));
    }
}