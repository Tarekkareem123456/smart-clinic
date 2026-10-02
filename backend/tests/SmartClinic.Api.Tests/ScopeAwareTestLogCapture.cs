using Microsoft.Extensions.Logging;

namespace SmartClinic.Api.Tests;

/// <summary>
/// T-007 — in-memory <see cref="ILoggerProvider"/> that, unlike T-006's
/// <see cref="TestLogCapture"/>, also snapshots the active logging SCOPES
/// (via <see cref="ISupportExternalScope"/>) for every entry. It keeps the
/// ORIGINAL state/scope value objects (not stringified) so tests can assert
/// that no request-scoped object was placed into log state. No package added.
/// </summary>
public sealed class ScopeAwareTestLogCapture : ILoggerProvider, ISupportExternalScope
{
    public sealed record Entry(
        string Category,
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyList<KeyValuePair<string, object?>> State,
        IReadOnlyList<KeyValuePair<string, object?>> ScopeValues);

    private readonly List<Entry> _entries = new();
    private IExternalScopeProvider? _scopeProvider;

    public IReadOnlyList<Entry> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.ToList();
            }
        }
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    /// <summary>Everything an entry contains (message, exception, state, scopes) as one searchable string.</summary>
    public static string AllText(Entry entry)
    {
        var parts = new List<string> { entry.Message, entry.Exception?.ToString() ?? string.Empty };
        parts.AddRange(entry.State.Select(kv => $"{kv.Key}={kv.Value}"));
        parts.AddRange(entry.ScopeValues.Select(kv => $"{kv.Key}={kv.Value}"));
        return string.Join("\n", parts);
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ScopeAwareTestLogCapture _owner;
        private readonly string _category;

        public CapturingLogger(ScopeAwareTestLogCapture owner, string category)
        {
            _owner = owner;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            _owner._scopeProvider?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var structured = (state as IEnumerable<KeyValuePair<string, object?>>)?.ToList()
                ?? new List<KeyValuePair<string, object?>>();

            var scopes = new List<KeyValuePair<string, object?>>();
            _owner._scopeProvider?.ForEachScope(
                (scope, list) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        list.AddRange(pairs);
                    }
                },
                scopes);

            lock (_owner._entries)
            {
                _owner._entries.Add(new Entry(_category, logLevel, formatter(state, exception), exception, structured, scopes));
            }
        }
    }
}
