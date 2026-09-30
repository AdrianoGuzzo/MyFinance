using System.Text.RegularExpressions;

using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain.ValueObjects;

/// <summary>
/// Número da conta bancária (com dígito opcional, ex.: 12345-6).
/// <see cref="ToString"/> retorna o valor mascarado para evitar vazamento acidental em logs.
/// </summary>
public sealed partial record AccountNumber
{
    private AccountNumber(string value) => Value = value;

    /// <summary>Valor completo. Não deve ser registrado em logs.</summary>
    public string Value { get; }

    public static AccountNumber Create(string? value)
    {
        var normalized = (value ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        if (!Pattern().IsMatch(normalized))
        {
            throw new DomainException("Número de conta inválido. Use apenas dígitos e, opcionalmente, o dígito verificador (ex.: 12345-6).");
        }

        return new AccountNumber(normalized);
    }

    public string Masked()
    {
        var digits = Value.Replace("-", string.Empty, StringComparison.Ordinal);
        return digits.Length <= 4 ? new string('*', digits.Length) : $"****{digits[^4..]}";
    }

    public override string ToString() => Masked();

    [GeneratedRegex("^[0-9]{1,20}(-[0-9X])?$")]
    private static partial Regex Pattern();
}