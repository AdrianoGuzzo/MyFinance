using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.ValueObjects;

/// <summary>
/// Últimos 4 dígitos do cartão. O número completo nunca é armazenado.
/// </summary>
public sealed record LastFourDigits
{
    private LastFourDigits(string value) => Value = value;

    public string Value { get; }

    public static LastFourDigits Create(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length != 4 || !trimmed.All(char.IsAsciiDigit))
        {
            throw new DomainException("Informe exatamente os 4 últimos dígitos do cartão.");
        }

        return new LastFourDigits(trimmed);
    }

    public override string ToString() => $"•••• {Value}";
}