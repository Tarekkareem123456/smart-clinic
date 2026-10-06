namespace SmartClinic.Application.Configuration;

/// <summary>
/// Explicit classification required for every registered configuration definition.
/// This is intentionally not an allow-list of configuration keys.
/// </summary>
public enum ConfigurationClassification
{
    Invariant = 1,
    NonInvariant = 2
}