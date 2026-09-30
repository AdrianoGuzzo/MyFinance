using Avalonia;
using Avalonia.Media;

namespace MyFinance.Desktop.Controls;

/// <summary>Barras agrupadas em pares (ex.: receitas x despesas por mês).</summary>
public sealed class BarChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<BarGroup>?> ItemsProperty =
        AvaloniaProperty.Register<BarChart, IReadOnlyList<BarGroup>?>(nameof(Items));

    public static readonly StyledProperty<IBrush?> FirstBrushProperty =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(FirstBrush), Brushes.SeaGreen);

    public static readonly StyledProperty<IBrush?> SecondBrushProperty =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(SecondBrush), Brushes.IndianRed);

    static BarChart() => AffectsRender<BarChart>(ItemsProperty, FirstBrushProperty, SecondBrushProperty);

    public IReadOnlyList<BarGroup>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public IBrush? FirstBrush
    {
        get => GetValue(FirstBrushProperty);
        set => SetValue(FirstBrushProperty, value);
    }

    public IBrush? SecondBrush
    {
        get => GetValue(SecondBrushProperty);
        set => SetValue(SecondBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var items = Items ?? [];
        var area = PlotArea;
        if (items.Count == 0 || area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var (min, max) = Range(items.SelectMany(i => new[] { i.First, i.Second }));
        DrawValueAxis(context, min, max);

        var slot = area.Width / items.Count;
        var barWidth = Math.Min(24, slot * 0.3);
        var zero = ToY(0, min, max);

        for (var i = 0; i < items.Count; i++)
        {
            var center = area.Left + (slot * i) + (slot / 2);
            DrawBar(context, FirstBrush, center - barWidth - 1, barWidth, zero, ToY(items[i].First, min, max));
            DrawBar(context, SecondBrush, center + 1, barWidth, zero, ToY(items[i].Second, min, max));

            var label = Text(items[i].Label);
            context.DrawText(label, new Point(center - (label.Width / 2), area.Bottom + 4));
        }
    }

    private static void DrawBar(DrawingContext context, IBrush? brush, double x, double width, double zeroY, double valueY)
    {
        var top = Math.Min(zeroY, valueY);
        var height = Math.Max(1, Math.Abs(zeroY - valueY));
        context.DrawRectangle(brush, null, new Rect(x, top, width, height), 2, 2);
    }
}