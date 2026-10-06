namespace SmartClinic.Application.Configuration;

/// <summary>
/// Persistence boundary for configuration values.
///
/// Concrete persistence/schema details are intentionally deferred until the
/// approved ConfigurationSetting storage model is available.
/// </summary>
public interface IConfigurationRepository
{
    Task<string?> GetAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default);
}