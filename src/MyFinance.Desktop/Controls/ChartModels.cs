namespace MyFinance.Desktop.Controls;

/// <summary>Grupo de duas barras (ex.: receitas e despesas de um mês).</summary>
public sealed record BarGroup(string Label, double First, double Second);

public sealed record ChartPoint(string Label, double Value);

/// <summary>Coluna do <see cref="ColumnChart"/>: valor principal, parte empilhada opcional e texto da dica.</summary>
/// <param name="Highlight">Escreve o valor sobre a coluna (use em uma só: o mês em foco).</param>
public sealed record ColumnItem(string Label, double Primary, double Secondary, string Tooltip, bool Highlight = false);