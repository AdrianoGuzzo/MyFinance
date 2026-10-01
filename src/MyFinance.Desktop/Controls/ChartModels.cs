namespace MyFinance.Desktop.Controls;

/// <summary>Coluna do <see cref="ColumnChart"/>: valor principal, parte empilhada opcional e texto da dica.</summary>
/// <param name="Highlight">Escreve o valor sobre a coluna (use em uma só: o mês em foco).</param>
public sealed record ColumnItem(string Label, double Primary, double Secondary, string Tooltip, bool Highlight = false);