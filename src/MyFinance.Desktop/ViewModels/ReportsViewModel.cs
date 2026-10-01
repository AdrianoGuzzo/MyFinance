using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Installments;
using MyFinance.Application.Reports;
using MyFinance.Application.Strategy;
using MyFinance.Desktop.Services;
using MyFinance.Domain.Analysis;

namespace MyFinance.Desktop.ViewModels;

public enum ReportKind
{
    ByCategory = 1,
    BySubcategory = 2,
    ByMerchant = 3,
    Evolution = 4,
    Installments = 5,
    Recurring = 6,
}

/// <summary>Linha genérica de relatório: até cinco colunas de texto (a primeira à esquerda, as demais alinhadas à direita).</summary>
public sealed record ReportRow(string C1, string C2, string C3, string C4, string C5);

public sealed partial class ReportsViewModel(PageServices services) : PageViewModel(services)
{
    private static readonly Option<ReportKind>[] Kinds =
    [
        new(ReportKind.ByCategory, "Gastos por categoria"),
        new(ReportKind.BySubcategory, "Gastos por subcategoria"),
        new(ReportKind.ByMerchant, "Gastos por estabelecimento"),
        new(ReportKind.Evolution, "Evolução mensal"),
        new(ReportKind.Installments, "Parcelamentos"),
        new(ReportKind.Recurring, "Gastos recorrentes"),
    ];

    /// <summary>0 = período personalizado.</summary>
    private static readonly Option<int>[] Periods =
        [new(3, "Últimos 3 meses"), new(6, "Últimos 6 meses"), new(12, "Últimos 12 meses"), new(0, "Personalizado")];

    // Nuláveis: o ComboBox grava null na propriedade quando sua lista de itens é recarregada.
    [ObservableProperty]
    private Option<ReportKind>? _kind = Kinds[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomPeriod))]
    private Option<int>? _period = Periods[1];

    [ObservableProperty]
    private DateTime? _customFrom;

    [ObservableProperty]
    private DateTime? _customTo;

    [ObservableProperty]
    private ReportRow _headers = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    [ObservableProperty]
    private string _periodText = string.Empty;

    public override string Title => "Relatórios";

    public IReadOnlyList<Option<ReportKind>> KindOptions => Kinds;

    public IReadOnlyList<Option<int>> PeriodOptions => Periods;

    public bool IsCustomPeriod => Period?.Value == 0;

    public ObservableCollection<ReportRow> Rows { get; } = [];

    public bool IsEmpty => Rows.Count == 0;

    public override Task LoadAsync() => GenerateAsync();

    partial void OnKindChanged(Option<ReportKind>? value)
    {
        if (value is not null)
        {
            _ = GenerateAsync();
        }
    }

    partial void OnPeriodChanged(Option<int>? value)
    {
        if (value is { Value: > 0 })
        {
            _ = GenerateAsync();
        }
    }

    [RelayCommand]
    private Task GenerateAsync() => RunAsync(async () =>
    {
        var (from, to) = await PeriodAsync();
        PeriodText = from == to ? Format.LongMonth(from) : $"{Format.LongMonth(from)} a {Format.LongMonth(to)}";
        var rows = Kind?.Value switch
        {
            ReportKind.BySubcategory => await ByCategoryAsync(from, to, CategoryLevel.Leaf),
            ReportKind.ByMerchant => await ByMerchantAsync(from, to),
            ReportKind.Evolution => await EvolutionAsync(from, to),
            ReportKind.Installments => await InstallmentsAsync(),
            ReportKind.Recurring => await RecurringAsync(),
            _ => await ByCategoryAsync(from, to, CategoryLevel.Root),
        };

        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(IsEmpty));
    });

    private async Task<(DateOnly From, DateOnly To)> PeriodAsync()
    {
        if (IsCustomPeriod && CustomFrom is { } from && CustomTo is { } to)
        {
            return (DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
        }

        var months = Period?.Value is > 0 and var m ? m : 6;
        return await UseCases.RunAsync<ReportService, (DateOnly, DateOnly)>((s, ct) => s.LastMonthsAsync(months, ct));
    }

    private async Task<IEnumerable<ReportRow>> ByCategoryAsync(DateOnly from, DateOnly to, CategoryLevel level)
    {
        Headers = new("Categoria", "Total", "Percentual", "Média mensal", "Variação (último mês)");
        var rows = await UseCases.RunAsync<ReportService, IReadOnlyList<CategoryReportRow>>((s, ct) => s.ByCategoryAsync(from, to, level, ct));
        return rows.Select(r => new ReportRow(r.Name, Format.Money(r.Total), r.Percent.ToString("P1", CultureInfo.CurrentCulture),
            Format.Money(r.MonthlyAverage), r.Variation is { } v ? Format.SignedPercent(v) : "—"));
    }

    private async Task<IEnumerable<ReportRow>> ByMerchantAsync(DateOnly from, DateOnly to)
    {
        Headers = new("Estabelecimento", "Compras", "Total", "Média por compra", string.Empty);
        var rows = await UseCases.RunAsync<ReportService, IReadOnlyList<MerchantReportRow>>((s, ct) => s.ByMerchantAsync(from, to, ct));
        return rows.Select(r => new ReportRow(r.Merchant, r.Count.ToString(CultureInfo.CurrentCulture), Format.Money(r.Total), Format.Money(r.Average), string.Empty));
    }

    private async Task<IEnumerable<ReportRow>> EvolutionAsync(DateOnly from, DateOnly to)
    {
        Headers = new("Mês", "Total", "Média (meses anteriores)", "Variação (mês anterior)", string.Empty);
        var rows = await UseCases.RunAsync<ReportService, IReadOnlyList<MonthlyReportRow>>((s, ct) => s.EvolutionAsync(from, to, ct));
        return rows.Select(r => new ReportRow(Format.LongMonth(r.Month), Format.Money(r.Total),
            r.Average is { } a ? Format.Money(a) : "—", r.Variation is { } v ? Format.SignedPercent(v) : "—", string.Empty));
    }

    private async Task<IEnumerable<ReportRow>> InstallmentsAsync()
    {
        Headers = new("Compra", "Parcela", "Parcelas restantes", "Valor restante", "Termina");
        PeriodText = "Compras parceladas em andamento";
        var rows = await UseCases.RunAsync<InstallmentService, IReadOnlyList<InstallmentPurchaseDto>>((s, ct) => s.ListAsync(false, ct));
        return rows.Select(r => new ReportRow(r.Description, Format.Money(r.InstallmentAmount), r.RemainingCount.ToString(CultureInfo.CurrentCulture),
            Format.Money(r.RemainingAmount), Format.ShortMonth(r.LastInvoiceMonth)));
    }

    private async Task<IEnumerable<ReportRow>> RecurringAsync()
    {
        Headers = new("Estabelecimento", "Valor mensal", "Valor anual", "Classificação", "Categoria");
        var overview = await UseCases.RunAsync<RecurringExpenseService, RecurringOverviewDto>((s, ct) => s.GetAsync(false, ct));
        PeriodText = $"Gastos recorrentes detectados · total {Format.Money(overview.MonthlyTotal)}/mês ({Format.Money(overview.AnnualTotal)}/ano)";
        return overview.Items.Select(r => new ReportRow(r.Name, Format.Money(r.MonthlyAmount), Format.Money(r.AnnualAmount),
            Labels.For(r.Classification), r.CategoryName ?? "—"));
    }
}