using UniDesk.Models;

namespace UniDesk.Services;

public enum WeatherFailureReason
{
    None,
    LocationUnavailable,
    LocationPermissionDenied,
    ApiConfigurationMissing,
    NetworkUnavailable,
    InvalidCity,
    ApiRejected,
    Unknown
}

public interface IWeatherService
{
    WeatherFailureReason LastFailure { get; }

    Task<WeatherInfo?> GetWeatherAsync(
        string city,
        CancellationToken cancellationToken = default,
        bool notifyUser = true);

    Task<WeatherInfo?> GetCachedWeatherAsync();

    Task<WeatherInfo?> RefreshWeatherAsync(
        CancellationToken cancellationToken = default,
        bool notifyUser = true);

    void CancelRefresh();

    Task SetCityAsync(string city);

    Task<QWeatherValidationResult> ValidateApiKeyAsync(
        string apiKey,
        string? apiHost = null,
        CancellationToken cancellationToken = default);

    string GetEffectiveApiKey();
}
