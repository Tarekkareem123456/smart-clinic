namespace SmartClinic.Application.Configuration;

/// <summary>
/// Application boundary for recording configuration changes.
///
/// The authoritative Audit implementation and audit-failure recovery
/// semantics are intentionally outside T-101.
/// </summary>
public interface IConfigurationAuditWriter
{
    Task RecordConfigurationChangeAsync(
        string key,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default);
}