using UniDesk.Helpers;
using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

public class LocationProviderTests
{
    [Fact]
    public async Task ResolveCityAsync_WhenAmapUnavailable_ShouldReturnSavedCity()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("AutoLocation", "true");
        settings.SetValue("City", " 上海 ");
        using var apiClient = new QWeatherApiClient(settings);
        using var provider = new AmapUnavailableLocationProvider(settings, apiClient);

        var city = await provider.ResolveCityAsync();

        Assert.Equal("上海", city);
        Assert.Null(await provider.GetLocationAsync());
    }

    private sealed class AmapUnavailableLocationProvider(
        ISettingsService settingsService,
        QWeatherApiClient apiClient)
        : LocationProvider(settingsService, apiClient)
    {
        protected override Task<string?> GetCityByAmapIpAsync(CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
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
