namespace SmartClinic.Application.Configuration;

/// <summary>
/// Enforces the non-weakenable configuration invariant from SRS §19.4
/// and BR-CFG-001.
///
/// An invariant configuration definition can never be changed through the
/// configuration service, regardless of caller permissions.
///
/// This class deliberately does not derive its invariant list from CDR-001–014.
/// Phase 10 §26.1 is the Clinical Decision Register, not an invariant catalog.
/// </summary>
public sealed class InvariantGuard
{
    public void EnsureChangeAllowed(ConfigurationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Classification == ConfigurationClassification.Invariant)
        {
            throw new InvalidOperationException(
                $"Configuration key '{definition.Key}' is classified as an invariant and cannot be changed.");
        }
    }
}