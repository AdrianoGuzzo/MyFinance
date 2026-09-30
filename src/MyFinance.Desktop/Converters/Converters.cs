using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

using MyFinance.Desktop.Services;

namespace MyFinance.Desktop.Converters;

/// <summary>"#E67E22" → pincel; valor inválido ou vazio → transparente.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public static HexToBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && Color.TryParse(hex, out var color) ? new SolidColorBrush(color) : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Texto amigável para enums do domínio (ex.: TransactionKind.Refund → "Estorno").</summary>
public sealed class EnumLabelConverter : IValueConverter
{
    public static EnumLabelConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Labels.Describe(value);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Itens desativados aparecem esmaecidos.</summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public static BoolToOpacityConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false ? 0.45 : 1.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Recuo de subcategorias na lista (ParentCategoryId preenchido).</summary>
public sealed class SubcategoryIndentConverter : IValueConverter
{
    public static SubcategoryIndentConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? new Avalonia.Thickness(0) : new Avalonia.Thickness(24, 0, 0, 0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}