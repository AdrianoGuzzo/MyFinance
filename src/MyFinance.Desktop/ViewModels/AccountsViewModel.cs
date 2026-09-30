using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.Accounts;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

public sealed partial class AccountsViewModel(PageServices services) : PageViewModel(services)
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle), nameof(IsEditing))]
    [NotifyCanExecuteChangedFor(nameof(DeactivateCommand), nameof(ActivateCommand))]
    private AccountDto? _selected;

    [ObservableProperty]
    private bool _showInactive;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _bankName = string.Empty;

    // Nulável: o ComboBox pode gravar null ao (re)carregar seus itens.
    [ObservableProperty]
    private Option<AccountType>? _accountType = Options.AccountTypes[0];

    [ObservableProperty]
    private decimal? _initialBalance = 0m;

    [ObservableProperty]
    private string? _accountNumber;

    [ObservableProperty]
    private string? _agency;

    public override string Title => "Contas";

    public ObservableCollection<AccountDto> Accounts { get; } = [];

    public IReadOnlyList<Option<AccountType>> AccountTypes => Options.AccountTypes;

    public bool IsEditing => Selected is not null;

    public string FormTitle => Selected is null ? "Nova conta" : $"Editar: {Selected.Name}";

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync();

    partial void OnSelectedChanged(AccountDto? value)
    {
        if (value is null)
        {
            ClearForm();
            return;
        }

        Name = value.Name;
        BankName = value.BankName;
        AccountType = AccountTypes.First(t => t.Value == value.AccountType);
        InitialBalance = value.InitialBalance;
        AccountNumber = value.AccountNumber?.Value;
        Agency = value.Agency;
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
        var command = new SaveAccountCommand(Name, BankName, AccountType?.Value ?? Domain.Enums.AccountType.Checking, InitialBalance ?? 0m, AccountNumber, Agency);

        if (Selected is { } current)
        {
            await UseCases.RunAsync<AccountService>((s, ct) => s.UpdateAsync(current.Id, command, ct));
        }
        else
        {
            await UseCases.RunAsync<AccountService, Guid>((s, ct) => s.CreateAsync(command, ct));
        }

        StatusMessage = $"Conta \"{Name.Trim()}\" salva.";
        Selected = null;
        await ReloadAsync();
    });

    private bool CanDeactivate() => Selected is { IsActive: true };

    [RelayCommand(CanExecute = nameof(CanDeactivate))]
    private async Task DeactivateAsync()
    {
        var account = Selected!;
        var confirmed = await Dialogs.ConfirmAsync(
            "Desativar conta",
            $"Desativar a conta \"{account.Name}\"?\n\nEla deixará de aparecer no saldo total e na importação. Os lançamentos são mantidos e a conta pode ser reativada.",
            "Desativar",
            isDestructive: true);

        if (confirmed && await RunAsync(() => UseCases.RunAsync<AccountService>((s, ct) => s.DeactivateAsync(account.Id, ct))))
        {
            StatusMessage = $"Conta \"{account.Name}\" desativada.";
            Selected = null;
            await RunAsync(ReloadAsync);
        }
    }

    private bool CanActivate() => Selected is { IsActive: false };

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private Task ActivateAsync() => RunAsync(async () =>
    {
        var account = Selected!;
        await UseCases.RunAsync<AccountService>((s, ct) => s.ActivateAsync(account.Id, ct));
        StatusMessage = $"Conta \"{account.Name}\" reativada.";
        Selected = null;
        await ReloadAsync();
    });

    private async Task ReloadAsync()
    {
        var list = await UseCases.RunAsync<AccountService, IReadOnlyList<AccountDto>>((s, ct) => s.ListAsync(ShowInactive, ct));
        Accounts.Clear();
        foreach (var account in list)
        {
            Accounts.Add(account);
        }
    }

    private void ClearForm()
    {
        Name = string.Empty;
        BankName = string.Empty;
        AccountType = AccountTypes[0];
        InitialBalance = 0m;
        AccountNumber = null;
        Agency = null;
    }
}