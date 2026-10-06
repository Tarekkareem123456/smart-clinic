namespace SmartClinic.Application.Configuration;

/// <summary>
/// Describes a configuration definition and its explicit safety classification.
/// </summary>
public sealed record ConfigurationDefinition
{
    public string Key { get; }

    public ConfigurationClassification Classification { get; }

    public ConfigurationDefinition(
        string key,
        ConfigurationClassification classification)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Configuration key is required.",
                nameof(key));
        }

        if (!Enum.IsDefined(classification))
        {
            throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "A configuration definition must have an explicit valid classification.");
        }

        Key = key;
        Classification = classification;
    }
}