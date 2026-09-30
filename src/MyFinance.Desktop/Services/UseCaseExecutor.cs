using Microsoft.Extensions.DependencyInjection;

namespace MyFinance.Desktop.Services;

/// <summary>
/// Executa um caso de uso em um escopo de DI próprio (um DbContext novo por operação),
/// evitando que ViewModels de vida longa acumulem entidades rastreadas ou dados desatualizados.
/// </summary>
public interface IUseCaseExecutor
{
    Task<TResult> RunAsync<TService, TResult>(Func<TService, CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
        where TService : notnull;

    Task RunAsync<TService>(Func<TService, CancellationToken, Task> action, CancellationToken cancellationToken = default)
        where TService : notnull;
}

internal sealed class UseCaseExecutor(IServiceScopeFactory scopeFactory) : IUseCaseExecutor
{
    public async Task<TResult> RunAsync<TService, TResult>(
        Func<TService, CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(action);

        var scope = scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(true))
        {
            return await action(scope.ServiceProvider.GetRequiredService<TService>(), cancellationToken).ConfigureAwait(true);
        }
    }

    public Task RunAsync<TService>(Func<TService, CancellationToken, Task> action, CancellationToken cancellationToken = default)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(action);

        return RunAsync<TService, bool>(
            async (service, ct) =>
            {
                await action(service, ct).ConfigureAwait(true);
                return true;
            },
            cancellationToken);
    }
}