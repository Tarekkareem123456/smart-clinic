namespace SmartClinic.Infrastructure.Persistence;

/// <summary>A single append-only audit record for a polymorphic entity.</summary>
public sealed class AuditLogEntry
{
    public long Id { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AuditLogEntry() { }

    public AuditLogEntry(
        string entityType,
        string entityId,
        string? oldValue,
        string? newValue,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type is required.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(entityId))
            throw new ArgumentException("Entity ID is required.", nameof(entityId));
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Audit timestamp must be UTC.", nameof(createdAtUtc));

        EntityType = entityType;
        EntityId = entityId;
        OldValue = oldValue;
        NewValue = newValue;
        CreatedAtUtc = createdAtUtc;
    }
}
