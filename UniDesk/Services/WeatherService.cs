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
    private readonly SemaphoreSlim _cacheWriteLock = new(1, 1);
    private readonly SemaphoreSlim _cityChangeLock = new(1, 1);

    private WeatherInfo? _cachedWeather;
    private DateTime _lastFetchTime;
    private CancellationTokenSource? _refreshCts;
    private long _cityGeneration;
    private string? _selectedCity;
    private string? _persistedCity;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    public WeatherFailureReason LastFailure { get; private set; }

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
        _persistedCity = _settingsService.GetValue("City", "");
        _selectedCity = string.IsNullOrWhiteSpace(_persistedCity) ? null : _persistedCity;
    }

    public Task<WeatherInfo?> GetWeatherAsync(
        string city,
        CancellationToken cancellationToken = default,
        bool notifyUser = true)
    {
        return GetWeatherAsyncCore(city, cancellationToken, notifyUser, expectedRefreshCts: null);
    }

    private async Task<WeatherInfo?> GetWeatherAsyncCore(
        string city,
        CancellationToken cancellationToken,
        bool notifyUser,
        CancellationTokenSource? expectedRefreshCts)
    {
        var cityGeneration = GetCityGeneration();
        if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
        {
            return null;
        }

        LastFailure = WeatherFailureReason.None;
        var apiKey = _apiClient.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
            {
                return null;
            }

            LastFailure = WeatherFailureReason.ApiConfigurationMissing;
            if (notifyUser)
            {
                _notificationService.ShowWarningMessage(L("Weather.ApiKeyMissing", "请先在设置中配置和风天气 API Key"));
            }

            return await GetCurrentCachedWeatherAsync(
                city,
                cityGeneration,
                expectedRefreshCts,
                markExpired: false);
        }

        try
        {
            if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
            {
                return null;
            }

            var location = await GetCityLocationAsync(city, cancellationToken);
            if (location == null)
            {
                if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
                {
                    return null;
                }

                if (LastFailure == WeatherFailureReason.None)
                {
                    LastFailure = WeatherFailureReason.InvalidCity;
                }

                if (notifyUser)
                {
                    if (LastFailure == WeatherFailureReason.InvalidCity)
                    {
                        _notificationService.ShowWarningMessage(Format("Weather.CityNotFoundFormat", $"未找到城市: {city}", city));
                    }
                    else
                    {
                        _notificationService.ShowWarningMessage(L(GetFailureResourceKey(LastFailure), "天气服务暂时不可用"));
                    }
                }

                return await GetCurrentCachedWeatherAsync(
                    city,
                    cityGeneration,
                    expectedRefreshCts,
                    markExpired: false);
            }

            var locationId = location.Value.Id;

            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
            {
                return null;
            }

            var weatherResponse = await _apiClient.GetAsync(
                "/v7/weather/now",
                $"location={locationId}",
                cancellationToken,
                legacyHost: "devapi.qweather.com",
                legacyPath: "/v7/weather/now");
            var weatherResult = DeserializeJson<QWeatherNowResponse>(weatherResponse);

            if (weatherResult?.Code != "200")
            {
                if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
                {
                    return null;
                }

                LastFailure = weatherResult?.Code == "404"
                    ? WeatherFailureReason.InvalidCity
                    : WeatherFailureReason.ApiRejected;
                if (notifyUser)
                {
                    HandleApiError(weatherResult?.Code ?? "unknown", city);
                }

                return await GetCurrentCachedWeatherAsync(
                    city,
                    cityGeneration,
                    expectedRefreshCts,
                    markExpired: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
            {
                return null;
            }

            var forecastResponse = await _apiClient.GetAsync(
                "/v7/weather/3d",
                $"location={locationId}",
                cancellationToken,
                legacyHost: "devapi.qweather.com",
                legacyPath: "/v7/weather/3d");
            var forecastResult = DeserializeJson<QWeatherForecastResponse>(forecastResponse);
            var todayForecast = forecastResult?.Daily?.FirstOrDefault();

            if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
            {
                return null;
            }

            var airPath = $"/airquality/v1/current/{location.Value.Lat}/{location.Value.Lon}";
            var airResponse = await _apiClient.GetAsync(
                airPath,
                "",
                cancellationToken,
                legacyHost: "devapi.qweather.com",
                legacyPath: airPath);
            var airResult = DeserializeJson<QWeatherAirQualityResponse>(airResponse);

            if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
            {
                return null;
            }

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

            if (!await SaveCacheAsync(info, city, cityGeneration, expectedRefreshCts, cancellationToken))
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts)
                ? info
                : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
            {
                return null;
            }

            LastFailure = WeatherFailureReason.NetworkUnavailable;
            Logger.LogError(ex, "WeatherService.GetWeather.Network");
            if (notifyUser)
            {
                _notificationService.ShowWarningMessage(
                    L("Weather.NetworkRequestFailed", "天气服务暂时不可用，请稍后重试。"));
            }

            return await GetCurrentCachedWeatherAsync(
                city,
                cityGeneration,
                expectedRefreshCts,
                markExpired: true);
        }
        catch (Exception ex)
        {
            if (!IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts))
            {
                return null;
            }

            LastFailure = WeatherFailureReason.Unknown;
            Logger.LogError(ex, "WeatherService.GetWeather.Unknown");
            if (notifyUser)
            {
                _notificationService.ShowErrorMessage(
                    L("Weather.GetWeatherFailed", "无法获取天气，请稍后重试。"));
            }

            return await GetCurrentCachedWeatherAsync(
                city,
                cityGeneration,
                expectedRefreshCts,
                markExpired: true);
        }
    }

    public async Task<WeatherInfo?> GetCachedWeatherAsync()
    {
        return await GetCachedWeatherAsync(markExpired: false);
    }

    private async Task<WeatherInfo?> GetCachedWeatherAsync(bool markExpired)
    {
        await _cacheWriteLock.WaitAsync();
        try
        {
            lock (_refreshLock)
            {
                if (_cachedWeather != null && DateTime.Now - _lastFetchTime < _cacheDuration)
                {
                    if (IsCacheForSelectedCityNoLock(_cachedWeather))
                    {
                        if (markExpired)
                        {
                            _cachedWeather.IsExpired = true;
                        }

                        return _cachedWeather;
                    }
                }
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
                        lock (_refreshLock)
                        {
                            if (!IsCacheForSelectedCityNoLock(cached))
                            {
                                return null;
                            }

                            _cachedWeather = cached;
                            _lastFetchTime = cached.FetchTime;
                        }

                        return cached;
                    }
                }
                catch
                {
                }
            }

            return null;
        }
        finally
        {
            _cacheWriteLock.Release();
        }
    }

    public async Task<WeatherInfo?> RefreshWeatherAsync(
        CancellationToken cancellationToken = default,
        bool notifyUser = true)
    {
        var refreshCts = CreateRefreshToken(cancellationToken);
        var token = refreshCts.Token;
        var cityGeneration = GetCityGeneration();

        try
        {
            var city = await _locationProvider.ResolveCityAsync(token);
            if (string.IsNullOrEmpty(city))
            {
                var cached = await GetCachedWeatherAsync(markExpired: true);
                if (!IsCurrentRefreshRequest(cityGeneration, refreshCts))
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(cached?.City))
                {
                    return await GetWeatherAsyncCore(cached.City, token, notifyUser, refreshCts);
                }

                if (notifyUser)
                {
                    LastFailure = MapLocationFailure(_locationProvider.LastFailure);
                    _notificationService.ShowWarningMessage(
                        L(GetFailureResourceKey(LastFailure), "无法获取天气位置，请检查 Windows 定位权限或手动填写城市"));
                }

                if (!IsCurrentRefreshRequest(cityGeneration, refreshCts))
                {
                    return null;
                }

                LastFailure = MapLocationFailure(_locationProvider.LastFailure);

                return cached;
            }

            return await GetWeatherAsyncCore(city, token, notifyUser, refreshCts);
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentRefreshRequest(cityGeneration, refreshCts))
            {
                return null;
            }

            var cached = await GetCachedWeatherAsync();
            return IsCurrentRefreshRequest(cityGeneration, refreshCts)
                ? cached
                : null;
        }
        finally
        {
            ClearRefreshToken(refreshCts);
        }
    }

    public void CancelRefresh()
    {
        lock (_refreshLock)
        {
            _refreshCts?.Cancel();
        }
    }

    public async Task SetCityAsync(string city)
    {
        string? previousCity;
        long cityGeneration;
        lock (_refreshLock)
        {
            previousCity = _selectedCity;
            cityGeneration = ++_cityGeneration;
            _selectedCity = city;
            _refreshCts?.Cancel();
        }

        await _cityChangeLock.WaitAsync();
        try
        {
            lock (_refreshLock)
            {
                if (cityGeneration != _cityGeneration ||
                    !string.Equals(_selectedCity, city, StringComparison.Ordinal))
                {
                    return;
                }
            }

            try
            {
                await _settingsService.SetSettingAsync("City", city);
                lock (_refreshLock)
                {
                    _persistedCity = city;
                }
            }
            catch
            {
                lock (_refreshLock)
                {
                    if (cityGeneration == _cityGeneration &&
                        string.Equals(_selectedCity, city, StringComparison.Ordinal))
                    {
                        _selectedCity = string.IsNullOrWhiteSpace(_persistedCity)
                            ? previousCity
                            : _persistedCity;
                    }
                }

                throw;
            }

            await _cacheWriteLock.WaitAsync();
            try
            {
                lock (_refreshLock)
                {
                    _cachedWeather = null;
                    _lastFetchTime = DateTime.MinValue;
                }

                if (File.Exists(_cacheFilePath))
                {
                    try
                    {
                        File.Delete(_cacheFilePath);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Logger.LogError(ex, "WeatherService.SetCity.DeleteStaleCache");
                    }
                }
            }
            finally
            {
                _cacheWriteLock.Release();
            }
        }
        finally
        {
            _cityChangeLock.Release();
        }
    }

    public Task<QWeatherValidationResult> ValidateApiKeyAsync(
        string apiKey,
        string? apiHost = null,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.ValidateAsync(apiKey, apiHost, cancellationToken);
    }

    public string GetEffectiveApiKey() => _apiClient.GetApiKey();

    private CancellationTokenSource CreateRefreshToken(CancellationToken external)
    {
        lock (_refreshLock)
        {
            _refreshCts?.Cancel();
            var refreshCts = CancellationTokenSource.CreateLinkedTokenSource(external);
            _refreshCts = refreshCts;
            return refreshCts;
        }
    }

    private void ClearRefreshToken(CancellationTokenSource refreshCts)
    {
        lock (_refreshLock)
        {
            if (ReferenceEquals(_refreshCts, refreshCts))
            {
                _refreshCts = null;
            }
        }

        refreshCts.Dispose();
    }

    private long GetCityGeneration()
    {
        lock (_refreshLock)
        {
            return _cityGeneration;
        }
    }

    private bool IsCurrentWeatherRequest(
        string city,
        long cityGeneration,
        CancellationTokenSource? expectedRefreshCts)
    {
        lock (_refreshLock)
        {
            return IsCurrentWeatherRequestNoLock(city, cityGeneration, expectedRefreshCts);
        }
    }

    private bool IsCurrentWeatherRequestNoLock(
        string city,
        long cityGeneration,
        CancellationTokenSource? expectedRefreshCts)
    {
        return cityGeneration == _cityGeneration &&
            (_selectedCity == null || string.Equals(_selectedCity, city, StringComparison.Ordinal)) &&
            (expectedRefreshCts == null || ReferenceEquals(_refreshCts, expectedRefreshCts));
    }

    private bool IsCurrentRefreshRequest(long cityGeneration, CancellationTokenSource refreshCts)
    {
        lock (_refreshLock)
        {
            return cityGeneration == _cityGeneration && ReferenceEquals(_refreshCts, refreshCts);
        }
    }

    private bool IsCacheForSelectedCityNoLock(WeatherInfo cached) =>
        string.IsNullOrWhiteSpace(_selectedCity) ||
        string.Equals(cached.City, _selectedCity, StringComparison.Ordinal);

    private async Task<WeatherInfo?> GetCurrentCachedWeatherAsync(
        string city,
        long cityGeneration,
        CancellationTokenSource? expectedRefreshCts,
        bool markExpired)
    {
        var cached = await GetCachedWeatherAsync(markExpired);
        return IsCurrentWeatherRequest(city, cityGeneration, expectedRefreshCts) &&
               (cached == null || string.Equals(cached.City, city, StringComparison.Ordinal))
            ? cached
            : null;
    }

    private async Task<(string Id, string Lat, string Lon)?> GetCityLocationAsync(string city, CancellationToken cancellationToken)
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

        LastFailure = result?.Code == "404"
            ? WeatherFailureReason.InvalidCity
            : WeatherFailureReason.ApiRejected;

        return null;
    }

    private static WeatherFailureReason MapLocationFailure(LocationFailureReason failure) => failure switch
    {
        LocationFailureReason.PermissionDenied => WeatherFailureReason.LocationPermissionDenied,
        LocationFailureReason.ApiConfigurationMissing => WeatherFailureReason.ApiConfigurationMissing,
        LocationFailureReason.NetworkUnavailable => WeatherFailureReason.NetworkUnavailable,
        LocationFailureReason.ReverseLookupFailed => WeatherFailureReason.LocationUnavailable,
        _ => WeatherFailureReason.LocationUnavailable
    };

    internal static string GetFailureResourceKey(WeatherFailureReason failure) => failure switch
    {
        WeatherFailureReason.LocationPermissionDenied => "Weather.LocationPermissionDenied",
        WeatherFailureReason.ApiConfigurationMissing => "Weather.ApiConfigurationMissing",
        WeatherFailureReason.NetworkUnavailable => "Weather.NetworkUnavailable",
        WeatherFailureReason.InvalidCity => "Weather.InvalidCity",
        WeatherFailureReason.ApiRejected => "Weather.ApiRejected",
        WeatherFailureReason.Unknown => "Weather.UnknownFailure",
        _ => "Weather.LocationUnavailable"
    };

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

    private async Task<bool> SaveCacheAsync(
        WeatherInfo info,
        string city,
        long cityGeneration,
        CancellationTokenSource? expectedRefreshCts,
        CancellationToken cancellationToken)
    {
        await _cacheWriteLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(info);

            lock (_refreshLock)
            {
                if (!IsCurrentWeatherRequestNoLock(city, cityGeneration, expectedRefreshCts))
                {
                    return false;
                }
            }

            try
            {
                await File.WriteAllTextAsync(_cacheFilePath, json, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }

            lock (_refreshLock)
            {
                if (!IsCurrentWeatherRequestNoLock(city, cityGeneration, expectedRefreshCts))
                {
                    return false;
                }

                _cachedWeather = info;
                _lastFetchTime = DateTime.Now;
            }

            return true;
        }
        finally
        {
            _cacheWriteLock.Release();
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
        CancellationTokenSource? refreshCts;
        lock (_refreshLock)
        {
            refreshCts = _refreshCts;
            _refreshCts = null;
            refreshCts?.Cancel();
        }

        refreshCts?.Dispose();
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
