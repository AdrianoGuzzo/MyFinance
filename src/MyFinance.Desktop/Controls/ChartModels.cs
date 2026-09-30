namespace MyFinance.Desktop.Controls;

/// <summary>Grupo de duas barras (ex.: receitas e despesas de um mês).</summary>
public sealed record BarGroup(string Label, double First, double Second);

public sealed record ChartPoint(string Label, double Value);