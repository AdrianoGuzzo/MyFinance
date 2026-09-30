using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Tests.ValueObjects;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    [InlineData(null)]
    public void LastFourDigits_invalido_falha(string? value)
    {
        var act = () => LastFourDigits.Create(value);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void LastFourDigits_valido_exibe_mascarado()
    {
        LastFourDigits.Create(" 4321 ").ToString().Should().Be("•••• 4321");
    }

    [Theory]
    [InlineData("#FFF")]
    [InlineData("FFFFFF")]
    [InlineData("#GGGGGG")]
    public void HexColor_invalido_falha(string value)
    {
        var act = () => HexColor.Create(value);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void DayOfMonth_fora_do_intervalo_falha(int value)
    {
        var act = () => DayOfMonth.Create(value);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DayOfMonth_ajusta_para_ultimo_dia_do_mes()
    {
        DayOfMonth.Create(31).In(2026, 2).Should().Be(new DateOnly(2026, 2, 28));
        DayOfMonth.Create(31).In(2028, 2).Should().Be(new DateOnly(2028, 2, 29));
        DayOfMonth.Create(10).In(2026, 2).Should().Be(new DateOnly(2026, 2, 10));
    }

    [Fact]
    public void Sha256Hash_calcula_vetor_conhecido()
    {
        Sha256Hash.Compute("abc").Value
            .Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public async Task Sha256Hash_de_stream_igual_ao_de_bytes()
    {
        var bytes = "conteúdo do extrato"u8.ToArray();
        using var stream = new MemoryStream(bytes);

        var fromStream = await Sha256Hash.ComputeAsync(stream, TestContext.Current.CancellationToken);

        fromStream.Should().Be(Sha256Hash.Compute(bytes));
    }

    [Fact]
    public void Sha256Hash_FromHex_normaliza_e_valida()
    {
        var hex = new string('A', 64);

        Sha256Hash.FromHex(hex).Value.Should().Be(new string('a', 64));
        ((Action)(() => Sha256Hash.FromHex("xyz"))).Should().Throw<DomainException>();
    }
}