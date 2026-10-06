namespace SmartClinic.Application.Configuration;

/// <summary>
/// Application boundary for configuration definitions.
///
/// The concrete production configuration catalog remains outside this
/// contract and is not introduced by T-101.
/// </summary>
public interface IConfigurationRegistry
{
    IReadOnlyCollection<ConfigurationDefinition> GetDefinitions();

    ConfigurationDefinition GetRequired(string key);
}