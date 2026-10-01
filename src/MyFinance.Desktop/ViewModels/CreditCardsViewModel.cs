using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.CreditCards;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

public sealed partial class CreditCardsViewModel(PageServices services) : PageViewModel(services)
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle), nameof(IsEditing))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand), nameof(ActivateCommand))]
    private CreditCardDto? _selected;

    [ObservableProperty]
    private bool _showInactive;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _issuer = string.Empty;

    // Nulável: o ComboBox grava null na propriedade quando sua lista de itens é recarregada.
    [ObservableProperty]
    private Option<CardBrand>? _brand = Options.CardBrands[1];

    [ObservableProperty]
    private string _lastFourDigits = string.Empty;

    [ObservableProperty]
    private decimal? _creditLimit = 0m;

    [ObservableProperty]
    private decimal? _closingDay = 1;

    [ObservableProperty]
    private decimal? _dueDay = 10;

    public override string Title => "Cartões";

    public ObservableCollection<CreditCardDto> CreditCards { get; } = [];

    public IReadOnlyList<Option<CardBrand>> Brands => Options.CardBrands;

    public bool IsEditing => Selected is not null;

    public string FormTitle => Selected is null ? "Novo cartão" : $"Editar: {Selected.Name}";

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync();

    partial void OnSelectedChanged(CreditCardDto? value)
    {
        Name = value?.Name ?? string.Empty;
        Issuer = value?.Issuer ?? string.Empty;
        Brand = Brands.First(b => b.Value == (value?.Brand ?? CardBrand.Mastercard));
        LastFourDigits = value?.LastFourDigits ?? string.Empty;
        CreditLimit = value?.CreditLimit ?? 0m;
        ClosingDay = value?.ClosingDay ?? 1;
        DueDay = value?.DueDay ?? 10;
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var command = new SaveCreditCardCommand(
            Name, Issuer, Brand?.Value ?? CardBrand.Other, LastFourDigits, CreditLimit ?? 0m, (int)(ClosingDay ?? 0), (int)(DueDay ?? 0));

        if (Selected is { } current)
        {
            await UseCases.RunAsync<CreditCardService>((s, ct) => s.UpdateAsync(current.Id, command, ct));
        }
        else
        {
            await UseCases.RunAsync<CreditCardService, Guid>((s, ct) => s.CreateAsync(command, ct));
        }

        StatusMessage = $"Cartão \"{Name.Trim()}\" salvo.";
        Selected = null;
        await ReloadAsync();
    });

    private bool CanDeactivate() => Selected is { IsActive: true };

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private async Task DeactivateAsync()
    {
        var card = Selected!;
        var confirmed = await Dialogs.ConfirmAsync(
            "Desativar cartão",
            $"Desativar o cartão \"{card.Name}\"?\n\nEle deixará de aparecer no painel e na importação. Os lançamentos são mantidos.",
            "Desativar",
            isDestructive: true);

        if (confirmed && await RunAsync(() => UseCases.RunAsync<CreditCardService>((s, ct) => s.DeactivateAsync(card.Id, ct))))
        {
            StatusMessage = $"Cartão \"{card.Name}\" desativado.";
            Selected = null;
            await RunAsync(ReloadAsync);
        }
    }

    private bool CanActivate() => Selected is { IsActive: false };

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private Task ActivateAsync() => RunAsync(async () =>
    {
        var card = Selected!;
        await UseCases.RunAsync<CreditCardService>((s, ct) => s.ActivateAsync(card.Id, ct));
        StatusMessage = $"Cartão \"{card.Name}\" reativado.";
        Selected = null;
        await ReloadAsync();
    });

    private async Task ReloadAsync()
    {
        var list = await UseCases.RunAsync<CreditCardService, IReadOnlyList<CreditCardDto>>((s, ct) => s.ListAsync(ShowInactive, ct));
        CreditCards.Clear();
        foreach (var card in list)
        {
            CreditCards.Add(card);
        }
    }
}