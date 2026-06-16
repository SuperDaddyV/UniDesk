using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public class WeatherService : IWeatherService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly ILocationProvider _locationProvider;
    private readonly QWeatherApiClient _apiClient;
    private readonly ILocalizationService? _localizationService;
    private readonly string _cacheFilePath;
    private readonly object _refreshLock = new();

    private WeatherInfo? _cachedWeather;
    private DateTime _lastFetchTime;
    private CancellationTokenSource? _refreshCts;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    public WeatherService(
        ISettingsService settingsService,
        INotificationService notificationService,
        ILocationProvider locationProvider,
        QWeatherApiClient apiClient,
        string? cacheFilePath = null,
        ILocalizationService? localizationService = null)
    {
        _settingsService = settingsService;
        _notificationService = notificationService;
        _locationProvider = locationProvider;
        _apiClient = apiClient;
        _localizationService = localizationService;
        _cacheFilePath = cacheFilePath ?? Path.Combine(DirectoryHelper.DataDirectory, "weather_cache.json");
    }

    public async Task<WeatherInfo?> GetWeatherAsync(
        string city,
        CancellationToken cancellationToken = default,
        bool notifyUser = true)
    {
        var apiKey = _apiClient.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            if (notifyUser)
            {
                _notificationService.ShowWarningMessage(L("Weather.ApiKeyMissing", "请先在设置中配置和风天气 API Key"));
            }

            return await GetCachedWeatherAsync();
        }

        try
        {
            var location = await GetCityLocationAsync(city, cancellationToken);
            if (location == null)
            {
                if (notifyUser)
                {
                    _notificationService.ShowWarningMessage(Format("Weather.CityNotFoundFormat", $"未找到城市: {city}", city));
                }

                return await GetCachedWeatherAsync();
            }

            var locationId = location.Value.Id;

            cancellationToken.ThrowIfCancellationRequested();

            var weatherResponse = await _apiClient.GetAsync(
                "/v7/weather/now",
                $"location={locationId}",
                cancellationToken,
                legacyHost: "devapi.qweather.com",
                legacyPath: "/v7/weather/now");
            var weatherResult = DeserializeJson<QWeatherNowResponse>(weatherResponse);

            if (weatherResult?.Code != "200")
            {
                if (notifyUser)
                {
                    HandleApiError(weatherResult?.Code ?? "unknown", city);
                }

                return await GetCachedWeatherAsync(markExpired: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var forecastResponse = await _apiClient.GetAsync(
                "/v7/weather/3d",
                $"location={locationId}",
                cancellationToken,
                legacyHost: "devapi.qweather.com",
                legacyPath: "/v7/weather/3d");
            var forecastResult = DeserializeJson<QWeatherForecastResponse>(forecastResponse);
            var todayForecast = forecastResult?.Daily?.FirstOrDefault();

            var airPath = $"/airquality/v1/current/{location.Value.Lat}/{location.Value.Lon}";
            var airResponse = await _apiClient.GetAsync(
                airPath,
                "",
                cancellationToken,
                legacyHost: "devapi.qweather.com",
                legacyPath: airPath);
            var airResult = DeserializeJson<QWeatherAirQualityResponse>(airResponse);

            var info = new WeatherInfo
            {
                City = city,
                Temperature = FormatTemperature(weatherResult.Now?.Temp),
                WeatherDesc = weatherResult.Now?.Text ?? "",
                MaxTemp = FormatTemperature(todayForecast?.TempMax),
                MinTemp = FormatTemperature(todayForecast?.TempMin),
                AirQuality = FormatAirQuality(airResult),
                Humidity = FormatHumidity(weatherResult.Now?.Humidity),
                IconCode = weatherResult.Now?.Icon ?? "",
                IconUri = string.Empty,
                FetchTime = DateTime.Now,
                IsExpired = false
            };

            _cachedWeather = info;
            _lastFetchTime = DateTime.Now;
            await SaveCacheAsync(info);

            return info;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            if (notifyUser)
            {
                _notificationService.ShowWarningMessage(Format("Weather.NetworkRequestFailedFormat", $"网络请求失败: {ex.Message}", ex.Message));
            }

            return await GetCachedWeatherAsync(markExpired: true);
        }
        catch (Exception ex)
        {
            if (notifyUser)
            {
                _notificationService.ShowErrorMessage(Format("Weather.GetWeatherFailedFormat", $"获取天气失败: {ex.Message}", ex.Message));
            }

            return await GetCachedWeatherAsync(markExpired: true);
        }
    }

    public async Task<WeatherInfo?> GetCachedWeatherAsync()
    {
        return await GetCachedWeatherAsync(markExpired: false);
    }

    private async Task<WeatherInfo?> GetCachedWeatherAsync(bool markExpired)
    {
        if (_cachedWeather != null && DateTime.Now - _lastFetchTime < _cacheDuration)
        {
            if (markExpired)
            {
                _cachedWeather.IsExpired = true;
            }

            return _cachedWeather;
        }

        if (File.Exists(_cacheFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_cacheFilePath);
                var cached = JsonSerializer.Deserialize<WeatherInfo>(json);
                if (cached != null)
                {
                    cached.IsExpired = markExpired || DateTime.Now - cached.FetchTime > _cacheDuration;
                    _cachedWeather = cached;
                    _lastFetchTime = cached.FetchTime;
                    return cached;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    public async Task<WeatherInfo?> RefreshWeatherAsync(
        CancellationToken cancellationToken = default,
        bool notifyUser = true)
    {
        var token = CreateRefreshToken(cancellationToken);

        try
        {
            var city = await _locationProvider.ResolveCityAsync(token);
            if (string.IsNullOrEmpty(city))
            {
                var cached = await GetCachedWeatherAsync(markExpired: true);
                if (!string.IsNullOrWhiteSpace(cached?.City))
                {
                    return await GetWeatherAsync(cached.City, token, notifyUser);
                }

                if (notifyUser)
                {
                    _notificationService.ShowWarningMessage(L("Weather.LocationFailed", "定位失败，请检查网络或在设置中指定城市"));
                }

                return cached;
            }

            return await GetWeatherAsync(city, token, notifyUser);
        }
        catch (OperationCanceledException)
        {
            return await GetCachedWeatherAsync();
        }
        finally
        {
            ClearRefreshToken();
        }
    }

    public void CancelRefresh()
    {
        lock (_refreshLock)
        {
            _refreshCts?.Cancel();
        }
    }

    public Task SetCityAsync(string city)
    {
        _settingsService.SetValue("City", city);
        _cachedWeather = null;
        _lastFetchTime = DateTime.MinValue;

        if (File.Exists(_cacheFilePath))
        {
            File.Delete(_cacheFilePath);
        }

        return Task.CompletedTask;
    }

    public Task<QWeatherValidationResult> ValidateApiKeyAsync(
        string apiKey,
        string? apiHost = null,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.ValidateAsync(apiKey, apiHost, cancellationToken);
    }

    public string GetEffectiveApiKey() => _apiClient.GetApiKey();

    private CancellationToken CreateRefreshToken(CancellationToken external)
    {
        lock (_refreshLock)
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(external);
            return _refreshCts.Token;
        }
    }

    private void ClearRefreshToken()
    {
        lock (_refreshLock)
        {
            _refreshCts?.Dispose();
            _refreshCts = null;
        }
    }

    private async Task<(string Id, string Lat, string Lon)?> GetCityLocationAsync(string city, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _apiClient.GetAsync(
                "/geo/v2/city/lookup",
                $"location={Uri.EscapeDataString(city)}",
                cancellationToken,
                legacyHost: "geoapi.qweather.com",
                legacyPath: "/v2/city/lookup");
            var result = DeserializeJson<QWeatherGeoResponse>(response);

            if (result?.Code == "200" && result.Locations?.Count > 0)
            {
                var loc = result.Locations[0];
                if (!string.IsNullOrEmpty(loc.Id) && !string.IsNullOrEmpty(loc.Lat) && !string.IsNullOrEmpty(loc.Lon))
                {
                    return (loc.Id, loc.Lat, loc.Lon);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }

        return null;
    }

    private void HandleApiError(string code, string city)
    {
        var message = code switch
        {
            "400" => L("Weather.ApiError.BadRequest", "请求错误"),
            "401" => L("Weather.ApiError.InvalidKey", "API Key 无效或已过期"),
            "402" => L("Weather.ApiError.LimitExceeded", "超过访问次数限制"),
            "403" => L("Weather.ApiError.Forbidden", "无访问权限"),
            "404" => Format("Weather.CityNotFoundFormat", $"未找到城市: {city}", city),
            "429" => L("Weather.ApiError.TooManyRequests", "请求过于频繁，请稍后再试"),
            "500" => L("Weather.ApiError.ServerError", "服务器内部错误"),
            _ => Format("Weather.ApiError.UnknownFormat", $"API 错误: {code}", code)
        };
        _notificationService.ShowWarningMessage(message);
    }

    private static string FormatTemperature(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : $"{value}°C";
    }

    private string FormatAirQuality(QWeatherAirQualityResponse? response)
    {
        if (response?.Indexes == null || response.Indexes.Count == 0)
        {
            return "";
        }

        var index = response.Indexes.FirstOrDefault(i => i.Code == "cn-mee")
            ?? response.Indexes.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.AqiDisplay));

        if (index == null)
        {
            return "";
        }

        var aqi = index.AqiDisplay ?? "";
        var category = index.Category ?? "";

        if (string.IsNullOrWhiteSpace(aqi))
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(category)
            ? Format("Weather.AirQualityFormat", $"空气 {aqi}", aqi)
            : Format("Weather.AirQualityWithCategoryFormat", $"空气{category} {aqi}", category, aqi);
    }

    private string FormatHumidity(string? humidity)
    {
        return string.IsNullOrWhiteSpace(humidity)
            ? ""
            : Format("Weather.HumidityFormat", $"湿度 {humidity}%", humidity);
    }

    private async Task SaveCacheAsync(WeatherInfo info)
    {
        try
        {
            var json = JsonSerializer.Serialize(info);
            await File.WriteAllTextAsync(_cacheFilePath, json);
        }
        catch
        {
        }
    }

    private static T? DeserializeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json);
    }

    public void Dispose()
    {
        CancelRefresh();
        ClearRefreshToken();
    }

    private string L(string key, string fallback) =>
        _localizationService?.GetString(key) ?? fallback;

    private string Format(string key, string fallback, params object?[] args) =>
        _localizationService?.Format(key, args) ?? fallback;

    private class QWeatherNowResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("now")]
        public QWeatherNow? Now { get; set; }
    }

    private class QWeatherNow
    {
        [JsonPropertyName("temp")]
        public string? Temp { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("humidity")]
        public string? Humidity { get; set; }
    }

    private class QWeatherForecastResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("daily")]
        public List<QWeatherDaily>? Daily { get; set; }
    }

    private class QWeatherDaily
    {
        [JsonPropertyName("tempMax")]
        public string? TempMax { get; set; }

        [JsonPropertyName("tempMin")]
        public string? TempMin { get; set; }
    }

    private class QWeatherAirQualityResponse
    {
        [JsonPropertyName("metadata")]
        public QWeatherAirMetadata? Metadata { get; set; }

        [JsonPropertyName("indexes")]
        public List<QWeatherAirIndex>? Indexes { get; set; }
    }

    private class QWeatherAirMetadata
    {
        [JsonPropertyName("tag")]
        public string? Tag { get; set; }
    }

    private class QWeatherAirIndex
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("aqi")]
        public double? Aqi { get; set; }

        [JsonPropertyName("aqiDisplay")]
        public string? AqiDisplay { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }
    }

    private class QWeatherGeoResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("location")]
        public List<QWeatherGeoLocation>? Locations { get; set; }
    }

    private class QWeatherGeoLocation
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }
    }
}
