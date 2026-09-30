using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using MyFinance.Application.Installments;
using MyFinance.Desktop.Controls;
using MyFinance.Desktop.Services;

namespace MyFinance.Desktop.ViewModels;

public sealed class InstallmentRowViewModel(InstallmentPurchaseDto purchase)
{
    public InstallmentPurchaseDto Purchase { get; } = purchase;

    /// <summary>"3/12" (parcela da fatura atual) ou "12x" quando a compra não tem parcela neste mês.</summary>
    public string InstallmentsText => Purchase.CurrentNumber is { } n ? $"{n}/{Purchase.InstallmentCount}" : $"{Purchase.InstallmentCount}x";

    public string EndText => Format.ShortMonth(Purchase.LastInvoiceMonth);
}

/// <summary>Compromissos futuros: compras parceladas e quanto das próximas faturas já está comprometido.</summary>
public sealed partial class InstallmentsViewModel(PageServices services) : PageViewModel(services)
{
    private const int ProjectionMonths = 12;

    [ObservableProperty]
    private bool _includeFinished;

    [ObservableProperty]
    private decimal _remainingTotal;

    [ObservableProperty]
    private decimal _monthlyTotal;

    [ObservableProperty]
    private IReadOnlyList<ColumnItem> _projection = [];

    public override string Title => "Parcelamentos";

    public ObservableCollection<InstallmentRowViewModel> Purchases { get; } = [];

    public ObservableCollection<CommitmentDto> Commitments { get; } = [];

    public bool IsEmpty => Purchases.Count == 0;

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    partial void OnIncludeFinishedChanged(bool value) => _ = LoadAsync();

    private async Task ReloadAsync()
    {
        var include = IncludeFinished;
        var purchases = await UseCases.RunAsync<InstallmentService, IReadOnlyList<InstallmentPurchaseDto>>((s, ct) => s.ListAsync(include, ct));
        var commitments = await UseCases.RunAsync<InstallmentService, IReadOnlyList<CommitmentDto>>((s, ct) => s.GetCommitmentsAsync(ProjectionMonths, ct));

        Purchases.Clear();
        foreach (var purchase in purchases)
        {
            Purchases.Add(new InstallmentRowViewModel(purchase));
        }

        Commitments.Clear();
        foreach (var commitment in commitments.Where(c => c.Amount > 0).Take(6))
        {
            Commitments.Add(commitment);
        }

        RemainingTotal = purchases.Sum(p => p.RemainingAmount);
        MonthlyTotal = purchases.Where(p => p.CurrentNumber is not null).Sum(p => p.InstallmentAmount);
        Projection = [.. commitments.Select(c => new ColumnItem(
            Format.ShortMonth(c.Month), (double)c.Amount, 0,
            $"{Format.LongMonth(c.Month)}: {Format.Money(c.Amount)} em {c.Installments} parcela(s)"))];
        OnPropertyChanged(nameof(IsEmpty));
    }
}