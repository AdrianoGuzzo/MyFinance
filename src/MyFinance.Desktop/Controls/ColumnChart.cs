using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MyFinance.Desktop.Controls;

/// <summary>
/// Colunas de uma série (ex.: gasto por mês), com uma segunda parte opcional empilhada no topo
/// (ex.: parcelas já comprometidas). Colunas de até 24px, topo arredondado e base reta, 2px de vão entre as partes,
/// valor escrito somente na coluna destacada e o detalhe de cada coluna na dica ao passar o mouse.
/// </summary>
public sealed class ColumnChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ColumnItem>?> ItemsProperty =
        AvaloniaProperty.Register<ColumnChart, IReadOnlyList<ColumnItem>?>(nameof(Items));

    public static readonly StyledProperty<IBrush?> PrimaryBrushProperty =
        AvaloniaProperty.Register<ColumnChart, IBrush?>(nameof(PrimaryBrush), Brushes.SteelBlue);

    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<ColumnChart, IBrush?>(nameof(SecondaryBrush), Brushes.Coral);

    private const double MaxColumnWidth = 24;
    private const double Radius = 4;
    private const double Gap = 2;

    private int _hovered = -1;

    static ColumnChart() => AffectsRender<ColumnChart>(ItemsProperty, PrimaryBrushProperty, SecondaryBrushProperty);

    public IReadOnlyList<ColumnItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public IBrush? PrimaryBrush
    {
        get => GetValue(PrimaryBrushProperty);
        set => SetValue(PrimaryBrushProperty, value);
    }

    public IBrush? SecondaryBrush
    {
        get => GetValue(SecondaryBrushProperty);
        set => SetValue(SecondaryBrushProperty, value);
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

        var (min, max) = Range(items.Select(i => Math.Max(0, i.Primary) + Math.Max(0, i.Secondary)));
        DrawValueAxis(context, min, max);

        var slot = area.Width / items.Count;
        var width = Math.Min(MaxColumnWidth, slot * 0.5);
        var zero = ToY(0, min, max);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var x = area.Left + (slot * i) + ((slot - width) / 2);
            var primaryTop = ToY(Math.Max(0, item.Primary), min, max);
            var totalTop = ToY(Math.Max(0, item.Primary) + Math.Max(0, item.Secondary), min, max);
            var hasSecondary = item.Secondary > 0;

            if (i == _hovered)
            {
                context.DrawRectangle(GridBrush, null, new Rect(area.Left + (slot * i), area.Top, slot, area.Height));
            }

            if (item.Primary > 0)
            {
                DrawColumn(context, PrimaryBrush, x, width, zero, primaryTop, roundTop: !hasSecondary);
            }

            if (hasSecondary)
            {
                var bottom = item.Primary > 0 ? primaryTop - Gap : zero;
                DrawColumn(context, SecondaryBrush, x, width, bottom, Math.Min(totalTop, bottom - 1), roundTop: true);
            }

            var label = Text(item.Label);
            context.DrawText(label, new Point(x + (width / 2) - (label.Width / 2), area.Bottom + 4));

            if (item.Highlight && item.Primary + item.Secondary > 0)
            {
                var value = Text(FormatCompact(item.Primary + item.Secondary));
                context.DrawText(value, new Point(x + (width / 2) - (value.Width / 2), Math.Max(0, totalTop - value.Height - 2)));
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var items = Items ?? [];
        var area = PlotArea;
        var x = e.GetPosition(this).X;
        var index = items.Count == 0 || x < area.Left || x > area.Right ? -1 : Math.Min(items.Count - 1, (int)((x - area.Left) / (area.Width / items.Count)));

        if (index == _hovered)
        {
            return;
        }

        _hovered = index;
        ToolTip.SetTip(this, index >= 0 ? items[index].Tooltip : null);
        ToolTip.SetIsOpen(this, index >= 0);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = -1;
        ToolTip.SetIsOpen(this, false);
        InvalidateVisual();
    }

    /// <summary>Coluna com base reta e, se <paramref name="roundTop"/>, cantos superiores arredondados.</summary>
    private static void DrawColumn(DrawingContext context, IBrush? brush, double x, double width, double bottom, double top, bool roundTop)
    {
        var height = bottom - top;
        if (height < 1)
        {
            return;
        }

        var radius = roundTop ? Math.Min(Radius, Math.Min(width / 2, height)) : 0;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(x, bottom), isFilled: true);
            g.LineTo(new Point(x, top + radius));
            if (radius > 0)
            {
                g.ArcTo(new Point(x + radius, top), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                g.LineTo(new Point(x + width - radius, top));
                g.ArcTo(new Point(x + width, top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            }
            else
            {
                g.LineTo(new Point(x + width, top));
            }

            g.LineTo(new Point(x + width, bottom));
            g.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, null, geometry);
    }
}