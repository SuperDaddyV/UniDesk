using System.Globalization;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;
using Xunit;

namespace UniDesk.Tests;

public class TimeWeatherViewModelTests
{
    [Fact]
    public void ClockAndCalendar_ShouldFormatAndNavigate()
    {
        var clock = new FakeClockService { CurrentTime = new DateTime(2026, 7, 11, 14, 5, 0) };
        using var viewModel = CreateViewModel(clock, new FakeWeatherService());

        clock.Raise();
        viewModel.ToggleCalendarPopupCommand.Execute(null);
        var currentTitle = viewModel.CalendarMonthTitle;
        viewModel.NextCalendarMonthCommand.Execute(null);

        Assert.Equal("14:05", viewModel.ClockTimeText);
        Assert.Equal("Jul 11, 2026", viewModel.ClockDateText);
        Assert.NotEmpty(viewModel.CalendarDays);
        Assert.NotEqual(currentTitle, viewModel.CalendarMonthTitle);
    }

    [Fact]
    public async Task InitializeAsync_ShouldApplyCachedWeather()
    {
        var weather = new FakeWeatherService
        {
            Cached = new WeatherInfo { City = "Shanghai", Temperature = "30°C", WeatherDesc = "Sunny" },
            ApiKey = string.Empty
        };
        using var viewModel = CreateViewModel(new FakeClockService(), weather);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasWeatherData);
        Assert.Equal("Shanghai", viewModel.WeatherCity);
        Assert.Equal("30°C", viewModel.WeatherTemperature);
        Assert.Equal("Weather.ConfigureApiKey", viewModel.WeatherStatusMessage);
    }

    [Fact]
    public async Task RefreshCommand_WhenRefreshFails_ShouldKeepCachedState()
    {
        var weather = new FakeWeatherService
        {
            Cached = new WeatherInfo { City = "Cached", Temperature = "20°C" },
            ApiKey = "key",
            RefreshResult = null
        };
        using var viewModel = CreateViewModel(new FakeClockService(), weather);

        await viewModel.RefreshWeatherCommand.ExecuteAsync(null);

        Assert.Equal("Cached", viewModel.WeatherCity);
        Assert.False(viewModel.IsWeatherLoading);
    }

    [Fact]
    public void Dispose_ShouldStopClockAndIgnoreFurtherTicks()
    {
        var clock = new FakeClockService { CurrentTime = new DateTime(2026, 1, 1, 10, 0, 0) };
        var viewModel = CreateViewModel(clock, new FakeWeatherService());
        clock.Raise();

        viewModel.Dispose();
        clock.CurrentTime = new DateTime(2026, 1, 1, 11, 0, 0);
        clock.Raise();

        Assert.Equal("10:00", viewModel.ClockTimeText);
        Assert.True(clock.Stopped);
    }

    private static TimeWeatherViewModel CreateViewModel(IClockService clock, IWeatherService weather) =>
        new(
            clock,
            weather,
            new NoOpNotificationService(),
            new TestLocalizationService(),
            startWeatherTimer: false);

    private sealed class FakeClockService : IClockService
    {
        public DateTime CurrentTime { get; set; } = new(2026, 7, 11, 12, 0, 0);
        public event Action? TimeChanged;
        public bool Stopped { get; private set; }
        public void Start() { }
        public void Stop() => Stopped = true;
        public void Raise() => TimeChanged?.Invoke();
    }

    private sealed class FakeWeatherService : IWeatherService
    {
        public WeatherInfo? Cached { get; set; }
        public WeatherInfo? RefreshResult { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public Task<WeatherInfo?> GetWeatherAsync(string city, CancellationToken cancellationToken = default, bool notifyUser = true) => Task.FromResult(RefreshResult);
        public Task<WeatherInfo?> GetCachedWeatherAsync() => Task.FromResult(Cached);
        public Task<WeatherInfo?> RefreshWeatherAsync(CancellationToken cancellationToken = default, bool notifyUser = true) => Task.FromResult(RefreshResult);
        public void CancelRefresh() { }
        public Task SetCityAsync(string city) => Task.CompletedTask;
        public Task<QWeatherValidationResult> ValidateApiKeyAsync(string apiKey, string? apiHost = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(QWeatherValidationResult.Fail("not used"));
        public string GetEffectiveApiKey() => ApiKey;
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => false;
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public event EventHandler? LanguageChanged;
        public string CurrentLanguage => "en-US";
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("en-US");
        public IReadOnlyList<LanguageOption> SupportedLanguages => [];
        public void Initialize(ISettingsService settingsService) { }
        public string NormalizeLanguage(string? language) => "en-US";
        public void SetLanguage(string? language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
        public string GetString(string key) => key;
        public string Format(string key, params object?[] args) => key;
    }
}
