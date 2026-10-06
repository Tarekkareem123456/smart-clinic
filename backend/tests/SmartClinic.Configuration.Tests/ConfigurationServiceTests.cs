using SmartClinic.Application.Configuration;
using Xunit;

namespace SmartClinic.Configuration.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public async Task Set_rejects_invariant_before_repository_write()
    {
        var registry = new TestRegistry(
            new ConfigurationDefinition(
                "test.invariant",
                ConfigurationClassification.Invariant));

        var repository = new TestRepository();
        var audit = new TestAuditWriter();

        var service = new ConfigurationService(
            registry,
            repository,
            audit,
            new InvariantGuard());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetAsync("test.invariant", "new-value"));

        Assert.False(repository.WriteOccurred);
        Assert.False(audit.Recorded);
    }

    [Fact]
    public async Task Set_writes_value_and_records_audit_for_non_invariant()
    {
        var registry = new TestRegistry(
            new ConfigurationDefinition(
                "test.non-invariant",
                ConfigurationClassification.NonInvariant));

        var repository = new TestRepository
        {
            CurrentValue = "old-value"
        };

        var audit = new TestAuditWriter();

        var service = new ConfigurationService(
            registry,
            repository,
            audit,
            new InvariantGuard());

        await service.SetAsync("test.non-invariant", "new-value");

        Assert.True(repository.WriteOccurred);
        Assert.Equal("new-value", repository.CurrentValue);

        Assert.True(audit.Recorded);
        Assert.Equal("test.non-invariant", audit.Key);
        Assert.Equal("old-value", audit.OldValue);
        Assert.Equal("new-value", audit.NewValue);
    }

    private sealed class TestRegistry : IConfigurationRegistry
    {
        private readonly ConfigurationDefinition _definition;

        public TestRegistry(ConfigurationDefinition definition)
        {
            _definition = definition;
        }

        public IReadOnlyCollection<ConfigurationDefinition> GetDefinitions()
            => [_definition];

        public ConfigurationDefinition GetRequired(string key)
        {
            if (!string.Equals(key, _definition.Key, StringComparison.Ordinal))
            {
                throw new KeyNotFoundException(key);
            }

            return _definition;
        }
    }

    private sealed class TestRepository : IConfigurationRepository
    {
        public string? CurrentValue { get; set; }

        public bool WriteOccurred { get; private set; }

        public Task<string?> GetAsync(
            string key,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentValue);

        public Task SetAsync(
            string key,
            string value,
            CancellationToken cancellationToken = default)
        {
            WriteOccurred = true;
            CurrentValue = value;
            return Task.CompletedTask;
        }
    }

    private sealed class TestAuditWriter : IConfigurationAuditWriter
    {
        public bool Recorded { get; private set; }

        public string? Key { get; private set; }

        public string? OldValue { get; private set; }

        public string? NewValue { get; private set; }

        public Task RecordConfigurationChangeAsync(
            string key,
            string? oldValue,
            string? newValue,
            CancellationToken cancellationToken = default)
        {
            Recorded = true;
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;

            return Task.CompletedTask;
        }
    }
}