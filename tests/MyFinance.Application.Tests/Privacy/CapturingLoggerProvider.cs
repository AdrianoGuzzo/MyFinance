using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace MyFinance.Application.Tests.Privacy;

/// <summary>Guarda mensagem formatada, propriedades estruturadas e exceção de cada registro.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new Logger(_entries);

    public void Dispose() { }

    private sealed class Logger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(" ", pairs.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty;
            entries.Enqueue($"{eventId.Name} {formatter(state, exception)} {properties} {exception}");
        }
    }
}