namespace MyFinance.Application.Common;

public static class TimeProviderExtensions
{
    /// <summary>Data local de hoje (o "mês atual" e a fatura aberta usam a data local).</summary>
    public static DateOnly Today(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
    }

    public static DateTime UtcNow(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return timeProvider.GetUtcNow().UtcDateTime;
    }
}
