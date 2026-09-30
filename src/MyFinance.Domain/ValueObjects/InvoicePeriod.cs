namespace MyFinance.Domain.ValueObjects;

/// <summary>
/// Período de uma fatura de cartão: compras em [<see cref="StartDate"/>, <see cref="ClosingDate"/>).
/// </summary>
public sealed record InvoicePeriod(DateOnly StartDate, DateOnly ClosingDate, DateOnly DueDate)
{
    /// <summary>Mês de referência da fatura (mês de vencimento, como os bancos exibem).</summary>
    public DateOnly ReferenceMonth => new(DueDate.Year, DueDate.Month, 1);

    public bool Contains(DateOnly date) => date >= StartDate && date < ClosingDate;
}