using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

public class QWeatherApiClientTests
{
    [Theory]
    [InlineData("abc1234xyz.def.qweatherapi.com", "abc1234xyz.def.qweatherapi.com")]
    [InlineData("https://ABC1234XYZ.def.qweatherapi.com", "abc1234xyz.def.qweatherapi.com")]
    [InlineData("https://abc1234xyz.def.qweatherapi.com:443", "abc1234xyz.def.qweatherapi.com")]
    public void TryNormalizeHost_OfficialDedicatedHost_ReturnsCanonicalHost(
        string input,
        string expected)
    {
        var valid = QWeatherApiClient.TryNormalizeHost(input, out var normalized);

        Assert.True(valid);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("http://abc.def.qweatherapi.com")]
    [InlineData("https://abc.def.qweatherapi.com/path")]
    [InlineData("https://abc.def.qweatherapi.com?x=1")]
    [InlineData("https://user@abc.def.qweatherapi.com")]
    [InlineData("https://abc.def.qweatherapi.com:444")]
    [InlineData("qweatherapi.com")]
    [InlineData("evilqweatherapi.com")]
    [InlineData("qweatherapi.com.attacker.example")]
    [InlineData("127.0.0.1")]
    public void TryNormalizeHost_NonOfficialOrAmbiguousHost_Rejects(string input)
    {
        Assert.False(QWeatherApiClient.TryNormalizeHost(input, out _));
    }

    [Theory]
    [InlineData("user-key", "")]
    [InlineData("", "abc.def.qweatherapi.com")]
    [InlineData("user-key", "attacker.example")]
    public void IncompleteOrInvalidUserCredentials_DoNotFallBackToBundledCredentials(
        string apiKey,
        string apiHost)
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("WeatherApiKey", apiKey);
        settings.SetValue("WeatherApiHost", apiHost);
        using var client = new QWeatherApiClient(settings);

        Assert.False(client.IsUsingBuiltInDefaults);
        Assert.Equal(string.Empty, client.GetApiKey());
        Assert.Equal(string.Empty, client.GetApiHost());
    }

    private sealed class InMemorySettingsService : ISettingsService
    {
        private readonly Dictionary<string, string?> _values = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public Task<string?> GetSettingAsync(string key) => Task.FromResult(GetSetting(key));
        public string? GetSetting(string key) => _values.GetValueOrDefault(key);
        public Task SetSettingAsync(string key, string? value)
        {
            SetSetting(key, value);
            return Task.CompletedTask;
        }

        public void SetSetting(string key, string? value) => _values[key] = value;
        public T GetSetting<T>(string key, T defaultValue) => defaultValue;
        public string GetValue(string key, string defaultValue) => GetSetting(key) ?? defaultValue;
        public void SetValue(string key, string value) => SetSetting(key, value);
        public void InvalidateCache() => _values.Clear();
        public Task FlushPendingSavesAsync() => Task.CompletedTask;
    }
}
