using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.Extensions.Logging;

using MyFinance.Application.Common.Logging;
using MyFinance.Desktop.Services;

namespace MyFinance.Desktop.ViewModels;

/// <summary>Dependências comuns das telas.</summary>
public sealed partial class PageServices(IUseCaseExecutor useCases, IDialogService dialogs, ILogger<PageServices> logger)
{
    public IUseCaseExecutor UseCases { get; } = useCases;

    public IDialogService Dialogs { get; } = dialogs;

    public async Task HandleErrorAsync(Exception exception)
    {
        var error = ErrorMessages.Describe(exception);
        if (error.IsTechnical)
        {
            LogTechnicalError(logger, exception);
        }

        await Dialogs.ShowErrorAsync(error.Title, error.Message).ConfigureAwait(true);
    }

    [LoggerMessage(EventId = LogEvents.TechnicalError, EventName = nameof(LogEvents.TechnicalError), Level = LogLevel.Error,
        Message = "Erro inesperado em uma operação da interface")]
    private static partial void LogTechnicalError(ILogger logger, Exception exception);
}

/// <summary>
/// Base das telas: indicador de ocupado, mensagem de status e tratamento uniforme de erros.
/// Nenhuma exceção de uma operação da tela derruba a aplicação.
/// </summary>
public abstract partial class PageViewModel(PageServices services) : ViewModelBase
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyText;

    [ObservableProperty]
    private string? _statusMessage;

    public abstract string Title { get; }

    protected IUseCaseExecutor UseCases => services.UseCases;

    protected IDialogService Dialogs => services.Dialogs;

    /// <summary>Chamado sempre que a tela é exibida.</summary>
    public virtual Task LoadAsync() => Task.CompletedTask;

    /// <returns><c>true</c> se a operação terminou sem erro.</returns>
    protected async Task<bool> RunAsync(Func<Task> action, string? busyText = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        IsBusy = true;
        BusyText = busyText;
        try
        {
            await action().ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            await services.HandleErrorAsync(ex).ConfigureAwait(true);
            return false;
        }
        finally
        {
            IsBusy = false;
            BusyText = null;
        }
    }
}