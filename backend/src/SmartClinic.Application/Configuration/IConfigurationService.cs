namespace SmartClinic.Application.Configuration;

/// <summary>
/// Single application write/read boundary for configuration.
/// </summary>
public interface IConfigurationService
{
    Task<string?> GetAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default);
}