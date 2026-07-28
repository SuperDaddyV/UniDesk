using UniDesk.Helpers;
using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

public class LocationProviderTests
{
    [Fact]
    public async Task ResolveCityAsync_WhenWindowsLocationUnavailable_ShouldReturnSavedCity()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("AutoLocation", "true");
        settings.SetValue("City", " 上海 ");
        using var apiClient = new QWeatherApiClient(settings);
        using var provider = new WindowsLocationUnavailableProvider(settings, apiClient);

        var city = await provider.ResolveCityAsync();

        Assert.Equal("上海", city);
        Assert.Null(await provider.GetLocationAsync());
    }

    [Fact]
    public async Task ResolveCityAsync_WhenAutoLocationSettingIsMissing_ShouldNotRequestLocation()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("City", "北京");
        using var apiClient = new QWeatherApiClient(settings);
        using var provider = new TrackingLocationProvider(settings, apiClient);

        var city = await provider.ResolveCityAsync();

        Assert.Equal("北京", city);
        Assert.Equal(0, provider.LocationRequestCount);
    }

    [Theory]
    [InlineData("??")]
    [InlineData("？？")]
    [InlineData("--")]
    [InlineData("  ...  ")]
    [InlineData("~~~")]
    public async Task ResolveCityAsync_WhenSavedCityIsLegacyPlaceholder_ShouldReturnNull(string city)
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("AutoLocation", "false");
        settings.SetValue("City", city);
        using var apiClient = new QWeatherApiClient(settings);
        using var provider = new WindowsLocationUnavailableProvider(settings, apiClient);

        var resolved = await provider.ResolveCityAsync();

        Assert.Null(resolved);
    }

    private sealed class WindowsLocationUnavailableProvider(
        ISettingsService settingsService,
        QWeatherApiClient apiClient)
        : LocationProvider(settingsService, apiClient)
    {
        public override Task<(double Latitude, double Longitude)?> GetLocationAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(double Latitude, double Longitude)?>(null);
    }

    private sealed class TrackingLocationProvider(
        ISettingsService settingsService,
        QWeatherApiClient apiClient)
        : LocationProvider(settingsService, apiClient)
    {
        public int LocationRequestCount { get; private set; }

        public override Task<(double Latitude, double Longitude)?> GetLocationAsync(
            CancellationToken cancellationToken = default)
        {
            LocationRequestCount++;
            return Task.FromResult<(double Latitude, double Longitude)?>(null);
        }
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
    }
}
