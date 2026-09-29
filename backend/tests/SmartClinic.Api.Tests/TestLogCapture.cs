using Microsoft.Extensions.Logging;

namespace SmartClinic.Api.Tests;

/// <summary>
/// T-006 — a tiny, dependency-free <see cref="ILoggerProvider"/> that just
/// records every log entry in memory, so a test can assert that the
/// correlationId returned in an error response body is the SAME value
/// that was logged server-side for that request. No logging package is
/// added for this — it is a ~30-line hand-rolled test double.
/// </summary>
public sealed class TestLogCapture : ILoggerProvider
{
    public sealed record Entry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyList<KeyValuePair<string, object?>> State);

    private readonly List<Entry> _entries = new();
    public IReadOnlyList<Entry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly TestLogCapture _owner;

        public CapturingLogger(TestLogCapture owner) => _owner = owner;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var structured = state as IReadOnlyList<KeyValuePair<string, object?>>
                ?? Array.Empty<KeyValuePair<string, object?>>();

            // Materialize the structured log state immediately.
            // Some ASP.NET Core logging states can reference request features
            // that are disposed after the request completes.
            var snapshot = structured
                .Select(kv => new KeyValuePair<string, object?>(
                    kv.Key,
                    kv.Value?.ToString()))
                .ToArray();

            var message = formatter(state, exception);

            lock (_owner._entries)
            {
                _owner._entries.Add(
                    new Entry(
                        logLevel,
                        message,
                        exception,
                        snapshot));
            }
        }
    }
}
