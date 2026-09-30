using Avalonia;
using Avalonia.Media;

namespace MyFinance.Desktop.Controls;

/// <summary>Linha simples com marcadores (ex.: evolução do saldo).</summary>
public sealed class LineChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<ChartPoint>?>(nameof(Points));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LineBrush), Brushes.SteelBlue);

    static LineChart() => AffectsRender<LineChart>(PointsProperty, LineBrushProperty);

    public IReadOnlyList<ChartPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var points = Points ?? [];
        var area = PlotArea;
        if (points.Count == 0 || area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var (min, max) = Range(points.Select(p => p.Value));
        DrawValueAxis(context, min, max);

        var slot = area.Width / points.Count;
        var positions = points
            .Select((p, i) => new Point(area.Left + (slot * i) + (slot / 2), ToY(p.Value, min, max)))
            .ToList();

        var pen = new Pen(LineBrush, 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        for (var i = 1; i < positions.Count; i++)
        {
            context.DrawLine(pen, positions[i - 1], positions[i]);
        }

        for (var i = 0; i < positions.Count; i++)
        {
            context.DrawEllipse(LineBrush, null, positions[i], 4, 4);

            var label = Text(points[i].Label);
            context.DrawText(label, new Point(positions[i].X - (label.Width / 2), area.Bottom + 4));
        }
    }
}