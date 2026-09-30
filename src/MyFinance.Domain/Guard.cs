using MyFinance.Domain.Exceptions;

namespace MyFinance.Domain;

/// <summary>
/// Validações reutilizadas pelas entidades. Mensagens em português, prontas para o usuário.
/// </summary>
internal static class Guard
{
    public static string Required(string? value, int maxLength, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{fieldName} é obrigatório.");
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new DomainException($"{fieldName} deve ter no máximo {maxLength} caracteres.");
    }

    public static string? Optional(string? value, int maxLength, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new DomainException($"{fieldName} deve ter no máximo {maxLength} caracteres.");
    }

    /// <summary>Valores monetários com no máximo 2 casas decimais (sem arredondamento silencioso).</summary>
    public static decimal Money(decimal value, string fieldName) =>
        decimal.Round(value, 2) == value
            ? value
            : throw new DomainException($"{fieldName} deve ter no máximo 2 casas decimais.");

    public static TEnum Defined<TEnum>(TEnum value, string fieldName)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new DomainException($"{fieldName} inválido.");

    public static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("Datas de auditoria devem estar em UTC.", nameof(value));
}