using System.Globalization;

namespace MyFinance.Infrastructure.Persistence.Configurations;

internal static class CheckConstraints
{
    /// <summary><c>"Coluna" IN (1, 2, ...)</c> com os valores definidos no enum (enums são gravados como INTEGER).</summary>
    public static string EnumIn<TEnum>(string column)
        where TEnum : struct, Enum =>
        $"\"{column}\" IN ({string.Join(", ", Enum.GetValues<TEnum>().Select(v => Convert.ToInt32(v, CultureInfo.InvariantCulture)))})";
}