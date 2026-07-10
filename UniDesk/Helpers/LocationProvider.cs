using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Services;

namespace UniDesk.Helpers;

public interface ILocationProvider
{
    Task<(double Latitude, double Longitude)?> GetLocationAsync(CancellationToken cancellationToken = default);
    Task<string?> GetCityByCoordinatesAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    Task<string?> ResolveCityAsync(CancellationToken cancellationToken = default);
}

public class LocationProvider : ILocationProvider, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly QWeatherApiClient _apiClient;

    public LocationProvider(ISettingsService settingsService, QWeatherApiClient apiClient)
    {
        _settingsService = settingsService;
        _apiClient = apiClient;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<string?> ResolveCityAsync(CancellationToken cancellationToken = default)
    {
        var autoLocation = _settingsService.GetSetting("AutoLocation", true);
        if (autoLocation)
        {
            var cityByAmap = await GetCityByAmapIpAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(cityByAmap))
            {
                return cityByAmap;
            }

        }

        var savedCity = _settingsService.GetValue("City", "").Trim();
        return string.IsNullOrWhiteSpace(savedCity) ? null : savedCity;
    }

    /// <summary>
    /// 高德 IP 定位（需 Web 服务 Key）。不传 ip 时使用当前出口 IP。
    /// </summary>
    protected virtual async Task<string?> GetCityByAmapIpAsync(CancellationToken cancellationToken)
    {
        var amapKey = AppSecrets.AmapApiKey;
        if (string.IsNullOrEmpty(amapKey))
        {
            return null;
        }

        try
        {
            var url = $"https://restapi.amap.com/v3/ip?key={Uri.EscapeDataString(amapKey)}&output=json";
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            var result = JsonSerializer.Deserialize<AmapIpResponse>(response);
            if (result?.Status != "1")
            {
                return null;
            }

            return FormatRegionCity(result.City, result.Province);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task<(double Latitude, double Longitude)?> GetLocationAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<(double Latitude, double Longitude)?>(null);

    public async Task<string?> GetCityByCoordinatesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiClient.GetApiKey()))
            {
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
                return null;
            }

            var result = JsonSerializer.Deserialize<QWeatherGeoResponse>(response);
            if (result?.Code != "200" || result.Locations == null || result.Locations.Count == 0)
            {
                return null;
            }

            return FormatQWeatherLocation(result.Locations[0]);
        }
        catch (Exception)
        {
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

    private static string? FormatRegionCity(string? city, string? provinceOrAdm)
    {
        if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(provinceOrAdm))
        {
            return null;
        }

        var cityName = city?.Trim() ?? string.Empty;
        var region = provinceOrAdm?.Trim() ?? string.Empty;

        if (cityName is "局域网" or "[]" || region is "局域网")
        {
            return null;
        }

        if (string.IsNullOrEmpty(cityName) || cityName == "[]")
        {
            return TrimAdministrativeSuffix(region);
        }

        if (IsDistrictLevel(cityName) && !string.IsNullOrEmpty(region))
        {
            return TrimAdministrativeSuffix(region);
        }

        if (!string.IsNullOrEmpty(region) &&
            (cityName == region || cityName == region + "市" || region == cityName + "市"))
        {
            return TrimAdministrativeSuffix(region);
        }

        return TrimAdministrativeSuffix(cityName);
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

    private class AmapIpResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("province")]
        public string? Province { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }
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
        _httpClient.Dispose();
    }
}
