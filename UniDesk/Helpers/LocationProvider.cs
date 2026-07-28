using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Services;
using Windows.Devices.Geolocation;

namespace UniDesk.Helpers;

public interface ILocationProvider
{
    LocationFailureReason LastFailure { get; }
    Task<(double Latitude, double Longitude)?> GetLocationAsync(CancellationToken cancellationToken = default);
    Task<string?> GetCityByCoordinatesAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    Task<string?> ResolveCityAsync(CancellationToken cancellationToken = default);
}

public enum LocationFailureReason
{
    None,
    PermissionDenied,
    WindowsLocationUnavailable,
    ApiConfigurationMissing,
    NetworkUnavailable,
    ReverseLookupFailed
}

public static class WeatherCityNormalizer
{
    public static string? Normalize(string? city)
    {
        var normalized = city?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized.All(character =>
                   char.IsPunctuation(character) || char.IsSymbol(character))
            ? null
            : normalized;
    }
}

public class LocationProvider : ILocationProvider, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly QWeatherApiClient _apiClient;

    public LocationFailureReason LastFailure { get; private set; }

    public LocationProvider(ISettingsService settingsService, QWeatherApiClient apiClient)
    {
        _settingsService = settingsService;
        _apiClient = apiClient;
    }

    public async Task<string?> ResolveCityAsync(CancellationToken cancellationToken = default)
    {
        LastFailure = LocationFailureReason.None;
        var autoLocation = _settingsService.GetSetting("AutoLocation", false);
        if (autoLocation)
        {
            var coordinates = await GetLocationAsync(cancellationToken);
            if (coordinates.HasValue)
            {
                var city = await GetCityByCoordinatesAsync(
                    coordinates.Value.Latitude,
                    coordinates.Value.Longitude,
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(city))
                {
                    return city;
                }
            }
        }

        var savedCity = WeatherCityNormalizer.Normalize(
            _settingsService.GetValue("City", ""));
        if (savedCity != null)
        {
            LastFailure = LocationFailureReason.None;
            return savedCity;
        }

        if (LastFailure == LocationFailureReason.None)
        {
            LastFailure = LocationFailureReason.WindowsLocationUnavailable;
        }

        return null;
    }

    public virtual async Task<(double Latitude, double Longitude)?> GetLocationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                LastFailure = access == GeolocationAccessStatus.Denied
                    ? LocationFailureReason.PermissionDenied
                    : LocationFailureReason.WindowsLocationUnavailable;
                return null;
            }

            var locator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default
            };
            var position = await locator.GetGeopositionAsync(
                TimeSpan.FromMinutes(10),
                TimeSpan.FromSeconds(10)).AsTask(cancellationToken);
            var coordinate = position.Coordinate.Point.Position;
            LastFailure = LocationFailureReason.None;
            return (coordinate.Latitude, coordinate.Longitude);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            LastFailure = LocationFailureReason.PermissionDenied;
            return null;
        }
        catch
        {
            LastFailure = LocationFailureReason.WindowsLocationUnavailable;
            return null;
        }
    }

    public async Task<string?> GetCityByCoordinatesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiClient.GetApiKey()))
            {
                LastFailure = LocationFailureReason.ApiConfigurationMissing;
                return null;
            }

            var lon = longitude.ToString("F2", CultureInfo.InvariantCulture);
            var lat = latitude.ToString("F2", CultureInfo.InvariantCulture);
            var response = await _apiClient.GetAsync(
                "/geo/v2/city/lookup",
                $"location={lon},{lat}&lang=zh",
                cancellationToken,
                legacyHost: "geoapi.qweather.com",
                legacyPath: "/v2/city/lookup");
            if (string.IsNullOrWhiteSpace(response))
            {
                LastFailure = LocationFailureReason.ReverseLookupFailed;
                return null;
            }

            var result = JsonSerializer.Deserialize<QWeatherGeoResponse>(response);
            if (result?.Code != "200" || result.Locations == null || result.Locations.Count == 0)
            {
                LastFailure = LocationFailureReason.ReverseLookupFailed;
                return null;
            }

            var city = FormatQWeatherLocation(result.Locations[0]);
            LastFailure = city == null
                ? LocationFailureReason.ReverseLookupFailed
                : LocationFailureReason.None;
            return city;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            LastFailure = LocationFailureReason.NetworkUnavailable;
            return null;
        }
        catch
        {
            LastFailure = LocationFailureReason.ReverseLookupFailed;
            return null;
        }
    }

    private static string? FormatQWeatherLocation(QWeatherLocation loc)
    {
        if (!string.IsNullOrWhiteSpace(loc.Adm2))
        {
            return TrimAdministrativeSuffix(loc.Adm2);
        }

        if (!string.IsNullOrWhiteSpace(loc.Adm1))
        {
            return TrimAdministrativeSuffix(loc.Adm1);
        }

        if (string.IsNullOrWhiteSpace(loc.Name) || IsDistrictLevel(loc.Name))
        {
            return null;
        }

        return TrimAdministrativeSuffix(loc.Name);
    }

    private static bool IsDistrictLevel(string name)
    {
        return name.EndsWith("区", StringComparison.Ordinal)
            || name.EndsWith("县", StringComparison.Ordinal)
            || name.EndsWith("旗", StringComparison.Ordinal);
    }

    private static string TrimAdministrativeSuffix(string name)
    {
        name = name.Trim();
        if (name.EndsWith("特别行政区", StringComparison.Ordinal))
        {
            return name[..^5];
        }

        if (name.EndsWith("自治区", StringComparison.Ordinal))
        {
            return name[..^3];
        }

        if (name.EndsWith("市", StringComparison.Ordinal) || name.EndsWith("省", StringComparison.Ordinal))
        {
            return name[..^1];
        }

        return name;
    }

    private class QWeatherGeoResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("location")]
        public List<QWeatherLocation>? Locations { get; set; }
    }

    private class QWeatherLocation
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("adm1")]
        public string? Adm1 { get; set; }

        [JsonPropertyName("adm2")]
        public string? Adm2 { get; set; }
    }

    public void Dispose()
    {
    }
}
