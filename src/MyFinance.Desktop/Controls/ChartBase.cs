using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MyFinance.Desktop.Controls;

/// <summary>Base dos gráficos simples: margens, cores e texto de eixo.</summary>
public abstract class ChartBase : Control
{
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(LabelBrush), Brushes.Gray);

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(GridBrush), new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)));

    protected const double LeftMargin = 64;
    protected const double BottomMargin = 22;
    protected const double TopMargin = 8;
    protected const double RightMargin = 8;
    protected const double FontSize = 11;

    static ChartBase() => AffectsRender<ChartBase>(LabelBrushProperty, GridBrushProperty);

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    protected Rect PlotArea => new(
        LeftMargin,
        TopMargin,
        Math.Max(0, Bounds.Width - LeftMargin - RightMargin),
        Math.Max(0, Bounds.Height - TopMargin - BottomMargin));

    protected FormattedText Text(string text) => new(
        text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, FontSize, LabelBrush ?? Brushes.Gray);

    /// <summary>Linhas horizontais de referência com o valor à esquerda.</summary>
    protected void DrawValueAxis(DrawingContext context, double min, double max)
    {
        var area = PlotArea;
        var pen = new Pen(GridBrush ?? Brushes.LightGray, 1);

        for (var i = 0; i <= 4; i++)
        {
            var value = min + ((max - min) * i / 4);
            var y = ToY(value, min, max);
            context.DrawLine(pen, new Point(area.Left, y), new Point(area.Right, y));

            var label = Text(FormatCompact(value));
            context.DrawText(label, new Point(area.Left - label.Width - 6, y - (label.Height / 2)));
        }
    }

    protected double ToY(double value, double min, double max)
    {
        var area = PlotArea;
        return max <= min ? area.Bottom : area.Bottom - ((value - min) / (max - min) * area.Height);
    }

    /// <summary>1.234 → "1,2 mil"; valores menores em moeda sem centavos.</summary>
    protected static string FormatCompact(double value) => Math.Abs(value) >= 1000
        ? $"{value / 1000:0.##} mil"
        : value.ToString("C0", CultureInfo.CurrentCulture);

    /// <summary>
    /// Limites que incluem o zero, arredondados para 1, 2 ou 5 × 10ⁿ para que as 4 divisões
    /// do eixo tenham rótulos distintos e legíveis. Sem dados: 0 a R$ 100.
    /// </summary>
    protected static (double Min, double Max) Range(IEnumerable<double> values)
    {
        var list = values.DefaultIfEmpty(0).ToList();
        var min = Math.Min(0, list.Min());
        var max = Math.Max(0, list.Max());

        if (max - min < 0.01)
        {
            return (0, 100);
        }

        return (min < 0 ? -NiceCeiling(-min) : 0, max > 0 ? NiceCeiling(max) : 0);
    }

    private static double NiceCeiling(double value)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var fraction = value / magnitude;
        var nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * magnitude;
    }
}