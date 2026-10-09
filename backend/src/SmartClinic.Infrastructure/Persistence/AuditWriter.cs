using SmartClinic.Application.Configuration;

namespace SmartClinic.Infrastructure.Persistence;

/// <summary>Append-only persistence path for audit entries.</summary>
public sealed class AuditWriter
{
    private readonly SmartClinicDbContext _dbContext;

    public AuditWriter(SmartClinicDbContext dbContext) => _dbContext = dbContext;

    public Task AppendAuditEntryAsync(
        string entityType,
        string entityId,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        // The configuration adapter uses the configuration key as EntityId,
        // which is also the field name needed for name-based redaction. For
        // other polymorphic entities, EntityId is a record identifier and
        // must not be interpreted as a field name.
        var fieldName = string.Equals(entityType, "Configuration", StringComparison.Ordinal)
            ? entityId
            : null;
        var safeOldValue = AuditValueRedactor.Redact(fieldName, oldValue);
        var safeNewValue = AuditValueRedactor.Redact(fieldName, newValue);
        var entry = new AuditLogEntry(
            entityType,
            entityId,
            safeOldValue,
            safeNewValue,
            DateTime.UtcNow);

        _dbContext.AuditLogEntries.Add(entry);
        return Task.CompletedTask;
    }
}
