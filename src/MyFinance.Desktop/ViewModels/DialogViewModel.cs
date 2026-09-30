using CommunityToolkit.Mvvm.Input;

namespace MyFinance.Desktop.ViewModels;

public sealed partial class DialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DialogViewModel(string title, string message, string confirmText, string? cancelText, bool isError, bool isDestructive)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsError = isError;
        IsDestructive = isDestructive;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmText { get; }

    public string? CancelText { get; }

    public bool HasCancel => CancelText is not null;

    public bool IsError { get; }

    public bool IsDestructive { get; }

    public Task<bool> Result => _result.Task;

    [RelayCommand]
    private void Confirm() => _result.TrySetResult(true);

    [RelayCommand]
    private void Cancel() => _result.TrySetResult(false);
}