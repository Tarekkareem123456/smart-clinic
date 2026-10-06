using SmartClinic.Application.Configuration;
using Xunit;

namespace SmartClinic.Configuration.Tests;

/// <summary>
/// T-102 tests. Keys below are test-only placeholders, not catalog entries.
/// </summary>
public sealed class ConfigurationCacheTests
{
    private const string Key = "test.non-invariant";
    private const string InvariantKey = "test.invariant";
    private const string UnregisteredKey = "test.unregistered";

    [Fact]
    public async Task Second_read_is_served_from_cache()
    {
        var h = new Harness();
        h.Repo.Value = "v1";

        Assert.Equal("v1", await h.Service.GetAsync(Key));
        Assert.Equal("v1", await h.Service.GetAsync(Key));

        Assert.Equal(1, h.Repo.GetCalls);
    }

    [Fact]
    public async Task Missing_value_is_cached_as_null()
    {
        var h = new Harness();

        Assert.Null(await h.Service.GetAsync(Key));
        Assert.Null(await h.Service.GetAsync(Key));

        Assert.Equal(1, h.Repo.GetCalls);
    }

    [Fact]
    public async Task Write_invalidates_cache_so_next_read_sees_new_value()
    {
        var h = new Harness();
        h.Repo.Value = "old";
        await h.Service.GetAsync(Key);

        await h.Service.SetAsync(Key, "new");

        Assert.Equal("new", await h.Service.GetAsync(Key));
    }

    [Fact]
    public async Task Audited_old_value_comes_from_repository_not_cache()
    {
        var h = new Harness();
        h.Repo.Value = "A";
        await h.Service.GetAsync(Key);   // cache now holds "A"
        h.Repo.Value = "B";              // changed behind this process's back

        await h.Service.SetAsync(Key, "C");

        var change = Assert.Single(h.Audit.Changes);
        Assert.Equal((Key, "B", "C"), change);
    }

    [Fact]
    public async Task Unregistered_key_never_reaches_repository_or_cache()
    {
        var h = new Harness();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.GetAsync(UnregisteredKey));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.SetAsync(UnregisteredKey, "x"));

        Assert.Equal(0, h.Repo.GetCalls);
        Assert.Equal(0, h.Repo.SetCalls);
    }

    [Fact]
    public async Task Rejected_invariant_write_does_not_write_audit_or_disturb_cache()
    {
        var h = new Harness();
        h.Repo.Value = "I";
        await h.Service.GetAsync(InvariantKey);

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.SetAsync(InvariantKey, "x"));
        await h.Service.GetAsync(InvariantKey);

        Assert.Equal(1, h.Repo.GetCalls);
        Assert.Equal(0, h.Repo.SetCalls);
        Assert.Empty(h.Audit.Changes);
    }

    [Fact]
    public async Task Failed_repository_write_still_invalidates_cache()
    {
        var h = new Harness();
        h.Repo.Value = "A";
        await h.Service.GetAsync(Key);
        h.Repo.SetFailure = new IOException("write failed");

        await Assert.ThrowsAsync<IOException>(() => h.Service.SetAsync(Key, "C"));
        h.Repo.SetFailure = null;
        h.Repo.Value = "B";

        Assert.Equal("B", await h.Service.GetAsync(Key));
    }

    [Fact]
    public async Task Audit_failure_propagates_and_leaves_no_stale_cache()
    {
        var h = new Harness();
        h.Repo.Value = "A";
        await h.Service.GetAsync(Key);
        h.Audit.Failure = new InvalidOperationException("audit unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.SetAsync(Key, "C"));

        Assert.Equal("C", await h.Service.GetAsync(Key));
    }

    [Fact]
    public async Task Failed_read_is_not_cached()
    {
        var h = new Harness();
        h.Repo.Value = "v1";
        h.Repo.GetFailure = new IOException("read failed");

        await Assert.ThrowsAsync<IOException>(() => h.Service.GetAsync(Key));
        h.Repo.GetFailure = null;

        Assert.Equal("v1", await h.Service.GetAsync(Key));
    }

    [Fact]
    public async Task Read_racing_a_write_cannot_cache_the_pre_write_value()
    {
        var h = new Harness();
        h.Repo.Value = "old";
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Repo.NextReadGate = gate;

        var inFlightRead = h.Service.GetAsync(Key);   // loaded "old", paused
        await h.Service.SetAsync(Key, "new");         // write + invalidate
        gate.SetResult();
        await inFlightRead;                           // would have stored "old"

        Assert.Equal("new", await h.Service.GetAsync(Key));
    }

    // ---- test doubles -------------------------------------------------------

    private sealed class Harness
    {
        public FakeRepository Repo { get; } = new();
        public FakeAudit Audit { get; } = new();
        public ConfigurationService Service { get; }

        public Harness()
        {
            var registry = new FakeRegistry(
                new ConfigurationDefinition(Key, ConfigurationClassification.NonInvariant),
                new ConfigurationDefinition(InvariantKey, ConfigurationClassification.Invariant));

            Service = new ConfigurationService(
                registry,
                Repo,
                Audit,
                new InvariantGuard());
        }
    }

    private sealed class FakeRegistry : IConfigurationRegistry
    {
        private readonly Dictionary<string, ConfigurationDefinition> _definitions;

        public FakeRegistry(params ConfigurationDefinition[] definitions)
            => _definitions = definitions.ToDictionary(d => d.Key, StringComparer.Ordinal);

        public IReadOnlyCollection<ConfigurationDefinition> GetDefinitions() => _definitions.Values;

        public ConfigurationDefinition GetRequired(string key)
            => _definitions.TryGetValue(key, out var d)
                ? d
                : throw new KeyNotFoundException($"Configuration key '{key}' is not registered.");
    }

    private sealed class FakeRepository : IConfigurationRepository
    {
        public string? Value { get; set; }
        public int GetCalls { get; private set; }
        public int SetCalls { get; private set; }
        public Exception? GetFailure { get; set; }
        public Exception? SetFailure { get; set; }
        public TaskCompletionSource? NextReadGate { get; set; }

        public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            GetCalls++;

            if (GetFailure is not null)
            {
                throw GetFailure;
            }

            var snapshot = Value;
            var gate = NextReadGate;
            NextReadGate = null;

            if (gate is not null)
            {
                await gate.Task;
            }

            return snapshot;
        }

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            SetCalls++;

            if (SetFailure is not null)
            {
                throw SetFailure;
            }

            Value = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudit : IConfigurationAuditWriter
    {
        public List<(string Key, string? Old, string? New)> Changes { get; } = new();
        public Exception? Failure { get; set; }

        public Task RecordConfigurationChangeAsync(
            string key,
            string? oldValue,
            string? newValue,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Changes.Add((key, oldValue, newValue));
            return Task.CompletedTask;
        }
    }
}
