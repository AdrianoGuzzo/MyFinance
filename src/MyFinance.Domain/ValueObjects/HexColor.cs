using System.Text.RegularExpressions;

using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.ValueObjects;

public sealed partial record HexColor
{
    private HexColor(string value) => Value = value;

    public string Value { get; }

    public static HexColor Create(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!Pattern().IsMatch(trimmed))
        {
            throw new DomainException("Cor inválida. Use o formato #RRGGBB.");
        }

        return new HexColor(trimmed.ToUpperInvariant());
    }

    public override string ToString() => Value;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex Pattern();
}