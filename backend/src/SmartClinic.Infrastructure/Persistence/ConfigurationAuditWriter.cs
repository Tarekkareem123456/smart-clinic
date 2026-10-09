using SmartClinic.Application.Configuration;

namespace SmartClinic.Infrastructure.Persistence;

/// <summary>Preserves the T-101 configuration audit contract.</summary>
public sealed class ConfigurationAuditWriter : IConfigurationAuditWriter
{
    private readonly AuditWriter _auditWriter;

    public ConfigurationAuditWriter(AuditWriter auditWriter) => _auditWriter = auditWriter;

    public Task RecordConfigurationChangeAsync(
        string key,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default) =>
        _auditWriter.AppendAuditEntryAsync(
            "Configuration",
            key,
            oldValue,
            newValue,
            cancellationToken);
}
