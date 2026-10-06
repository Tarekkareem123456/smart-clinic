namespace SmartClinic.Application.Configuration;

/// <summary>
/// Single application path for configuration reads and writes.
///
/// Authorization is expected to be enforced by the application boundary
/// that invokes this service. Invariant protection is independent of those
/// permissions and is always enforced by InvariantGuard.
///
/// T-102: reads are served from a private in-process cache after the registry
/// check; writes always read the previous value from the repository (never the
/// cache, so the audited old value is authoritative) and invalidate the cached
/// key as soon as the repository write has been attempted. Freshness is
/// guaranteed for writes made through this service only.
/// </summary>
public sealed class ConfigurationService : IConfigurationService
{
    private readonly IConfigurationRegistry _registry;
    private readonly IConfigurationRepository _repository;
    private readonly IConfigurationAuditWriter _auditWriter;
    private readonly InvariantGuard _invariantGuard;
    private readonly ConfigurationValueCache _cache;

    public ConfigurationService(
        IConfigurationRegistry registry,
        IConfigurationRepository repository,
        IConfigurationAuditWriter auditWriter,
        InvariantGuard invariantGuard)
    {
        _registry = registry;
        _repository = repository;
        _auditWriter = auditWriter;
        _invariantGuard = invariantGuard;
        _cache = new ConfigurationValueCache();
    }

    public async Task<string?> GetAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var definition = _registry.GetRequired(key);

        if (_cache.TryGet(definition.Key, out var cached))
        {
            return cached;
        }

        var generation = _cache.Generation;

        var value = await _repository.GetAsync(
            definition.Key,
            cancellationToken);

        _cache.Set(definition.Key, value, generation);

        return value;
    }

    public async Task SetAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        var definition = _registry.GetRequired(key);

        _invariantGuard.EnsureChangeAllowed(definition);

        // Deliberately bypasses the cache: the audited old value must come
        // from the authoritative store.
        var oldValue = await _repository.GetAsync(
            definition.Key,
            cancellationToken);

        try
        {
            await _repository.SetAsync(
                definition.Key,
                value,
                cancellationToken);
        }
        finally
        {
            // Invalidate even if the write outcome is uncertain (exception),
            // and before auditing, so an audit failure can never leave a
            // stale cached value behind. Audit failure semantics are unchanged.
            _cache.Invalidate(definition.Key);
        }

        await _auditWriter.RecordConfigurationChangeAsync(
            definition.Key,
            oldValue,
            value,
            cancellationToken);
    }
}