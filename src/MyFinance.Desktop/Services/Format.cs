using System.Globalization;

namespace MyFinance.Desktop.Services;

/// <summary>Formatação de valores exibidos (cultura configurada em App:Culture, pt-BR por padrão).</summary>
public static class Format
{
    public static string Money(decimal value) => value.ToString("C", CultureInfo.CurrentCulture);

    /// <summary>0,108 → "+10,8%"; −0,25 → "−25,0%".</summary>
    public static string SignedPercent(decimal ratio)
    {
        var text = Math.Abs(ratio * 100).ToString("0.0", CultureInfo.CurrentCulture) + "%";
        return ratio > 0 ? "+" + text : ratio < 0 ? "−" + text : text;
    }

    /// <summary>"out/26".</summary>
    public static string ShortMonth(DateOnly month) => month.ToString("MMM/yy", CultureInfo.CurrentCulture);

    /// <summary>"Outubro de 2026".</summary>
    public static string LongMonth(DateOnly month)
    {
        var text = month.ToString("MMMM 'de' yyyy", CultureInfo.CurrentCulture);
        return CultureInfo.CurrentCulture.TextInfo.ToUpper(text[0]) + text[1..];
    }
}