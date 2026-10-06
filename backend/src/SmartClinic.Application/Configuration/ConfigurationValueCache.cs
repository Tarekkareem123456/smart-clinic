using System.Collections.Concurrent;

namespace SmartClinic.Application.Configuration;

/// <summary>
/// In-process cache of configuration values, keyed by the canonical key of a
/// registered ConfigurationDefinition.
///
/// Internal on purpose: it is only reachable through ConfigurationService, so
/// the registry check (GetRequired) is always the gate in front of it. No
/// Audit data or patient data has a path into this type.
///
/// Entries are removed only by invalidation on write. This type makes no
/// statement about changes made outside ConfigurationService.
///
/// Stale-store protection: a reader captures Generation before loading from the
/// repository and passes it to Set. Invalidate bumps the generation, so a read
/// that raced with a write can never re-insert the pre-write value.
/// </summary>
internal sealed class ConfigurationValueCache
{
    private readonly record struct Entry(string? Value);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private long _generation;

    public long Generation => Volatile.Read(ref _generation);

    public bool TryGet(string key, out string? value)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public void Set(string key, string? value, long generationAtLoad)
    {
        lock (_gate)
        {
            if (_generation != generationAtLoad)
            {
                return;
            }

            _entries[key] = new Entry(value);
        }
    }

    public void Invalidate(string key)
    {
        lock (_gate)
        {
            _generation++;
            _entries.TryRemove(key, out _);
        }
    }
}
