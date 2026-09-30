using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MyFinance.Application.CreditCards;
using MyFinance.Application.Imports;
using MyFinance.Desktop.Services;
using MyFinance.Domain.Enums;

namespace MyFinance.Desktop.ViewModels;

public sealed class ImportRowViewModel(ImportPreviewRow row)
{
    public string DateText => row.Date?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? "—";

    public string Description => string.IsNullOrEmpty(row.Description) ? "—" : row.Description;

    public decimal? Amount => row.Amount;

    public bool IsExpense => row.Amount < 0;

    public string InvoiceText => row.InvoiceMonth?.ToString("MM/yyyy", CultureInfo.CurrentCulture) ?? string.Empty;

    public string KindText => row.Kind is { } kind ? Labels.For(kind) : string.Empty;

    public string StatusText => Labels.For(row.Status);

    public string? Detail => row.Status == ImportTransactionStatus.Duplicate ? Labels.For(row.DuplicateReason) : row.Message;

    public bool IsNew => row.Status == ImportTransactionStatus.New;

    public bool IsDuplicate => row.Status == ImportTransactionStatus.Duplicate;

    public bool IsInvalid => row.Status == ImportTransactionStatus.Invalid;
}

/// <summary>
/// Fluxo: selecionar arquivo → identificar formato/ler → selecionar cartão → prévia → confirmar.
/// Nada é gravado antes de <see cref="ConfirmCommand"/>; cancelar apenas descarta a prévia.
/// </summary>
public sealed partial class ImportViewModel(PageServices services, IFilePickerService filePicker) : PageViewModel(services)
{
    private bool _suppressRefresh;

    // Cada atualização de prévia recebe uma versão; resultados de versões antigas são descartados.
    private int _previewVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile), nameof(FileSummary), nameof(PreviousImportMessage))]
    private ImportFileAnalysis? _analysis;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview), nameof(TotalText), nameof(NewText), nameof(DuplicatesText), nameof(ErrorsText))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private ImportPreview? _preview;

    [ObservableProperty]
    private CardOption? _selectedCard;

    [ObservableProperty]
    private bool _invertAmounts;

    public override string Title => "Importação";

    public ObservableCollection<CardOption> CardOptions { get; } = [];

    public ObservableCollection<ImportRowViewModel> Rows { get; } = [];

    public bool HasFile => Analysis is not null;

    public bool HasPreview => Preview is not null;

    public string FileSummary => Analysis is null
        ? string.Empty
        : $"{Analysis.FileName} · {Analysis.FileType.ToString().ToUpperInvariant()}";

    public string? PreviousImportMessage => Analysis?.PreviousImport is { } previous
        ? $"Este arquivo já foi importado.\nData da importação: {previous.ImportedAtUtc.ToLocalTime():dd/MM/yyyy}\nTransações: {previous.TransactionCount}\n\nVocê pode continuar: a prévia indica o que já existe e nada será duplicado."
        : null;

    public string TotalText => $"{Preview?.Summary.Total ?? 0} transações encontradas";

    public string NewText => $"{Preview?.Summary.New ?? 0} novas";

    public string DuplicatesText => $"{Preview?.Summary.Duplicates ?? 0} duplicadas";

    public string ErrorsText => $"{Preview?.Summary.Errors ?? 0} com erro";

    partial void OnSelectedCardChanged(CardOption? value)
    {
        if (!_suppressRefresh && value is not null)
        {
            InvalidatePreview();
            _ = RefreshPreviewAsync(invertAmounts: null);
        }
    }

    partial void OnInvertAmountsChanged(bool value)
    {
        if (!_suppressRefresh)
        {
            InvalidatePreview();
            _ = RefreshPreviewAsync(value);
        }
    }

    [RelayCommand]
    private async Task SelectFileAsync()
    {
        StatusMessage = null;
        PickedFile? file = null;
        if (!await RunAsync(async () => file = await filePicker.PickStatementAsync()) || file is null)
        {
            return;
        }

        Reset();
        await RunAsync(async () =>
        {
            var stream = await file.OpenReadAsync();
            await using (stream.ConfigureAwait(true))
            {
                Analysis = await UseCases.RunAsync<ImportService, ImportFileAnalysis>((s, ct) => s.AnalyzeAsync(file.Name, stream, ct));
            }

            await LoadCardsAsync();
        }, "Lendo arquivo...");

        if (Analysis is not null && SelectedCard is not null)
        {
            await RefreshPreviewAsync(invertAmounts: null);
        }
    }

    /// <summary>Só confirma a prévia atual, do cartão selecionado, com a tela livre.</summary>
    private bool CanConfirm() =>
        !IsBusy
        && Preview is { IsConfirmed: false, Summary.Total: > 0 } preview
        && SelectedCard?.Id == preview.CreditCardId;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy) or nameof(SelectedCard))
        {
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        if (!CanConfirm())
        {
            return;
        }

        var preview = Preview!;
        ImportOutcome? outcome = null;

        await RunAsync(
            async () => outcome = await UseCases.RunAsync<ImportService, ImportOutcome>((s, ct) => s.ConfirmAsync(preview, ct)),
            "Gravando lançamentos...");

        if (outcome is null)
        {
            return;
        }

        var summary = outcome.Summary;
        Reset();
        StatusMessage = $"Importação concluída: {summary.New} nova(s), {summary.Duplicates} duplicada(s), {summary.Errors} com erro.";
        await Dialogs.ShowMessageAsync(
            "Importação concluída",
            $"{summary.Total} transações encontradas\n\n{summary.New} novas\n{summary.Duplicates} duplicadas\n{summary.Errors} com erro");
    }

    [RelayCommand]
    private void Cancel()
    {
        Reset();
        StatusMessage = "Importação cancelada. Nenhum dado foi gravado.";
    }

    private async Task RefreshPreviewAsync(bool? invertAmounts)
    {
        if (Analysis is not { } analysis || SelectedCard?.Id is not { } cardId)
        {
            return;
        }

        var version = ++_previewVersion;
        await RunAsync(async () =>
        {
            var preview = await UseCases.RunAsync<ImportService, ImportPreview>(
                (s, ct) => s.PreviewAsync(analysis, cardId, invertAmounts, ct));

            if (version != _previewVersion)
            {
                return; // o usuário trocou o destino/sinais ou cancelou enquanto esta prévia era calculada
            }

            Preview = preview;
            _suppressRefresh = true;
            InvertAmounts = preview.AmountsInverted;
            _suppressRefresh = false;

            Rows.Clear();
            foreach (var row in preview.Rows)
            {
                Rows.Add(new ImportRowViewModel(row));
            }
        }, "Verificando duplicidades...");
    }

    private async Task LoadCardsAsync()
    {
        var cards = await UseCases.RunAsync<CreditCardService, IReadOnlyList<CreditCardDto>>((s, ct) => s.ListAsync(false, ct));

        _suppressRefresh = true;
        CardOptions.Clear();
        foreach (var card in cards)
        {
            CardOptions.Add(new CardOption(card.Id, $"{card.Name} •••• {card.LastFourDigits}"));
        }

        SelectedCard = CardOptions.FirstOrDefault(o => o.Id == Analysis?.SuggestedCreditCardId)
            ?? (CardOptions.Count == 1 ? CardOptions[0] : null);
        _suppressRefresh = false;

        if (CardOptions.Count == 0)
        {
            await Dialogs.ShowMessageAsync("Cadastre um cartão", "Antes de importar, cadastre o cartão de crédito da fatura.");
        }
    }

    /// <summary>A prévia exibida deixa de valer assim que o destino ou os sinais mudam.</summary>
    private void InvalidatePreview()
    {
        _previewVersion++;
        Preview = null;
        Rows.Clear();
    }

    private void Reset()
    {
        _previewVersion++;
        _suppressRefresh = true;
        Analysis = null;
        Preview = null;
        SelectedCard = null;
        InvertAmounts = false;
        CardOptions.Clear();
        Rows.Clear();
        _suppressRefresh = false;
    }
}