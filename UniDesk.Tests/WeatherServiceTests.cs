using System.IO;
using System.Net;
using System.Text.Json;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

public class WeatherServiceTests : IDisposable
{
    private readonly string _cachePath;

    public WeatherServiceTests()
    {
        _cachePath = Path.Combine(Path.GetTempPath(), $"UniDesk_weather_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_cachePath))
        {
            File.Delete(_cachePath);
        }
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithEmptyKey_ReturnsFalse()
    {
        var service = CreateWeatherService(new InMemorySettingsService());

        var result = await service.ValidateApiKeyAsync("");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetCachedWeatherAsync_WhenCacheMissing_ReturnsNull()
    {
        var service = CreateWeatherService(new InMemorySettingsService());

        var result = await service.GetCachedWeatherAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task WeatherCache_SerializeAndDeserialize_PreservesFields()
    {
        var info = new WeatherInfo
        {
            City = "北京",
            Temperature = "25°C",
            WeatherDesc = "晴",
            MaxTemp = "28°C",
            MinTemp = "18°C",
            AirQuality = "AQI 42 (优)",
            IconCode = "100",
            IconUri = string.Empty,
            FetchTime = DateTime.Now.AddMinutes(-40),
            IsExpired = true
        };

        var json = JsonSerializer.Serialize(info);
        await File.WriteAllTextAsync(_cachePath, json);

        var loaded = JsonSerializer.Deserialize<WeatherInfo>(await File.ReadAllTextAsync(_cachePath));

        Assert.NotNull(loaded);
        Assert.Equal("北京", loaded!.City);
        Assert.Equal("25°C", loaded.Temperature);
        Assert.Equal("晴", loaded.WeatherDesc);
        Assert.True(loaded.IsExpired);
    }

    [Fact]
    public async Task SetCityAsync_ClearsCachedWeather()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("City", "上海");
        var service = CreateWeatherService(settings);

        await service.SetCityAsync("广州");

        Assert.Equal("广州", settings.GetValue("City", ""));
        var cached = await service.GetCachedWeatherAsync();
        Assert.Null(cached);
    }

    [Fact]
    public async Task GetWeatherAsync_WhenHttpTransportFails_ShouldKeepNetworkUnavailableReason()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("WeatherApiKey", "test-key");
        settings.SetValue("WeatherApiHost", "test.qweatherapi.com");
        using var client = new HttpClient(new ThrowingHttpHandler());
        using var apiClient = new QWeatherApiClient(settings, client);
        using var service = new WeatherService(
            settings,
            new NoOpNotificationService(),
            new StubLocationProvider(),
            apiClient,
            _cachePath);

        var result = await service.GetWeatherAsync("北京", notifyUser: false);

        Assert.Null(result);
        Assert.Equal(WeatherFailureReason.NetworkUnavailable, service.LastFailure);
    }

    private WeatherService CreateWeatherService(InMemorySettingsService settings)
    {
        var notification = new NoOpNotificationService();
        var location = new StubLocationProvider();
        var apiClient = new QWeatherApiClient(settings);
        return new WeatherService(settings, notification, location, apiClient, _cachePath);
    }

    private sealed class InMemorySettingsService : ISettingsService
    {
        private readonly Dictionary<string, string?> _values = new();

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<string?> GetSettingAsync(string key) => Task.FromResult(GetSetting(key));

        public string? GetSetting(string key) => _values.TryGetValue(key, out var value) ? value : null;

        public Task SetSettingAsync(string key, string? value)
        {
            SetSetting(key, value);
            return Task.CompletedTask;
        }

        public void SetSetting(string key, string? value) => _values[key] = value;

        public T GetSetting<T>(string key, T defaultValue)
        {
            var value = GetSetting(key);
            if (value == null) return defaultValue;
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public string GetValue(string key, string defaultValue) => GetSetting(key) ?? defaultValue;

        public void SetValue(string key, string value) => SetSetting(key, value);

        public void InvalidateCache() => _values.Clear();

        public Task FlushPendingSavesAsync() => Task.CompletedTask;

        public Task SaveBatchAsync(IReadOnlyDictionary<string, string?> values)
        {
            foreach (var (key, value) in values) _values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => false;
    }

    private sealed class StubLocationProvider : ILocationProvider
    {
        public LocationFailureReason LastFailure => LocationFailureReason.WindowsLocationUnavailable;

        public Task<(double Latitude, double Longitude)?> GetLocationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<(double, double)?>(null);

        public Task<string?> GetCityByCoordinatesAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string?> ResolveCityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException(
                "forced transport failure",
                null,
                HttpStatusCode.ServiceUnavailable));
    }
}
