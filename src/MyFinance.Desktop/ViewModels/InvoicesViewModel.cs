using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Common;
using MyFinance.Application.CreditCards;
using MyFinance.Application.Invoices;
using MyFinance.Application.Transactions;
using MyFinance.Desktop.Services;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

public sealed class InvoiceRowViewModel(InvoiceDto invoice)
{
    public InvoiceDto Invoice { get; } = invoice;

    public string MonthText => Format.LongMonth(Invoice.ReferenceMonth);

    public string StatusText => Labels.For(Invoice.Status);

    public bool IsPaid => Invoice.Status == InvoiceStatus.Paid;

    public bool IsOverdue => Invoice.Status == InvoiceStatus.Overdue;

    public bool IsOpen => Invoice.Status == InvoiceStatus.Open;
}

/// <summary>Faturas por cartão: total (compras, tarifas e juros menos estornos), situação e lançamentos.</summary>
public sealed partial class InvoicesViewModel(PageServices services) : PageViewModel(services)
{
    private const int MaxItems = 500;
    private static readonly CardOption AllCards = new(null, "Todos os cartões");

    // Nuláveis: ComboBox/ListBox gravam null ao recarregar a lista de itens.
    [ObservableProperty]
    private CardOption? _selectedCard = AllCards;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectionTitle))]
    [NotifyCanExecuteChangedFor(nameof(MarkPaidCommand), nameof(MarkUnpaidCommand))]
    private InvoiceRowViewModel? _selected;

    public override string Title => "Faturas";

    public ObservableCollection<CardOption> CardOptions { get; } = [AllCards];

    public ObservableCollection<InvoiceRowViewModel> Invoices { get; } = [];

    public ObservableCollection<TransactionListItem> Items { get; } = [];

    public bool HasSelection => Selected is not null;

    public string SelectionTitle => Selected is { } row
        ? $"{row.Invoice.CreditCardName} · {row.MonthText} · vence {row.Invoice.DueDate:dd/MM/yyyy}"
        : string.Empty;

    public override Task LoadAsync() => RunAsync(async () =>
    {
        var cards = await UseCases.RunAsync<CreditCardService, IReadOnlyList<CreditCardDto>>((s, ct) => s.ListAsync(true, ct));
        var card = SelectedCard;
        CardOptions.Clear();
        CardOptions.Add(AllCards);
        foreach (var c in cards)
        {
            CardOptions.Add(new CardOption(c.Id, c.Name));
        }

        SelectedCard = CardOptions.FirstOrDefault(o => o == card) ?? AllCards;
        await ReloadAsync();
    });

    partial void OnSelectedCardChanged(CardOption? value)
    {
        if (value is not null && !IsBusy)
        {
            _ = RunAsync(ReloadAsync);
        }
    }

    partial void OnSelectedChanged(InvoiceRowViewModel? value) => _ = RunAsync(LoadItemsAsync);

    private bool CanMarkPaid() => Selected is { IsPaid: false, IsOpen: false };

    [RelayCommand(CanExecute = nameof(CanMarkPaid))]
    private Task MarkPaidAsync() => ChangeAsync((s, id, ct) => s.MarkPaidAsync(id, ct), "marcada como paga");

    private bool CanMarkUnpaid() => Selected is { IsPaid: true };

    [RelayCommand(CanExecute = nameof(CanMarkUnpaid))]
    private Task MarkUnpaidAsync() => ChangeAsync((s, id, ct) => s.MarkUnpaidAsync(id, ct), "marcada como não paga");

    private Task ChangeAsync(Func<InvoiceService, Guid, CancellationToken, Task> change, string done) => RunAsync(async () =>
    {
        var row = Selected!;
        await UseCases.RunAsync<InvoiceService>((s, ct) => change(s, row.Invoice.Id, ct));
        StatusMessage = $"Fatura de {row.MonthText} ({row.Invoice.CreditCardName}) {done}.";
        await ReloadAsync();
    });

    private async Task ReloadAsync()
    {
        var selectedId = Selected?.Invoice.Id;
        var cardId = SelectedCard?.Id;
        var list = await UseCases.RunAsync<InvoiceService, IReadOnlyList<InvoiceDto>>((s, ct) => s.ListAsync(cardId, ct));

        Invoices.Clear();
        foreach (var invoice in list.Where(i => i.TransactionCount > 0 || i.Status == InvoiceStatus.Open))
        {
            Invoices.Add(new InvoiceRowViewModel(invoice));
        }

        Selected = Invoices.FirstOrDefault(i => i.Invoice.Id == selectedId);
    }

    private async Task LoadItemsAsync()
    {
        Items.Clear();
        if (Selected is not { } row)
        {
            return;
        }

        var search = new TransactionSearch { InvoiceId = row.Invoice.Id, PageSize = MaxItems };
        var result = await UseCases.RunAsync<TransactionService, PagedResult<TransactionListItem>>((s, ct) => s.SearchAsync(search, ct));
        foreach (var item in result.Items)
        {
            Items.Add(item);
        }
    }
}