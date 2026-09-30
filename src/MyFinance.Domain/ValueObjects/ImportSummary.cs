namespace MyFinance.Domain.ValueObjects;

/// <summary>Ex.: 127 encontradas — 121 novas, 6 duplicadas, 0 com erro.</summary>
public sealed record ImportSummary(int Total, int New, int Duplicates, int Errors);