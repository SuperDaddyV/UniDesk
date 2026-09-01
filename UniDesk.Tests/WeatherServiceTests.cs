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
    public async Task SetCityAsync_WhenPersistenceFails_RestoresPreviousCityAndKeepsCache()
    {
        var settings = new InMemorySettingsService();
        using var client = new HttpClient(new ThrowingHttpHandler());
        using var apiClient = new QWeatherApiClient(settings, client);
        using var service = new WeatherService(
            settings,
            new NoOpNotificationService(),
            new StubLocationProvider(),
            apiClient,
            _cachePath);

        await service.SetCityAsync("北京");
        var cachedInfo = new WeatherInfo
        {
            City = "北京",
            Temperature = "25°C",
            WeatherDesc = "晴",
            FetchTime = DateTime.Now
        };
        await File.WriteAllTextAsync(_cachePath, JsonSerializer.Serialize(cachedInfo));

        settings.FailWrites = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetCityAsync("广州"));

        Assert.Equal("北京", settings.GetValue("City", ""));
        Assert.True(File.Exists(_cachePath));
        var result = await service.GetWeatherAsync("北京", notifyUser: false);

        Assert.NotNull(result);
        Assert.Equal("北京", result!.City);
    }

    [Fact]
    public async Task SetCityAsync_WhenOldCacheCannotBeDeleted_KeepsPersistedCityAndRejectsStaleCache()
    {
        using var testLogs = new TestLogDirectoryScope();
        var settings = new InMemorySettingsService();
        settings.SetValue("City", "北京");
        using var service = CreateWeatherService(settings);
        var cachedInfo = new WeatherInfo
        {
            City = "北京",
            Temperature = "25°C",
            WeatherDesc = "晴",
            FetchTime = DateTime.Now
        };
        await File.WriteAllTextAsync(_cachePath, JsonSerializer.Serialize(cachedInfo));

        using (File.Open(_cachePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await service.SetCityAsync("广州");
        }

        Assert.Equal("广州", settings.GetValue("City", ""));
        Assert.Null(await service.GetCachedWeatherAsync());
    }

    [Fact]
    public async Task GetWeatherAsync_WhenCachedCityDiffers_ShouldNotReturnStaleCity()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("City", "广州");
        settings.SetValue("WeatherApiKey", "incomplete-user-configuration");
        var cachedInfo = new WeatherInfo
        {
            City = "北京",
            Temperature = "25°C",
            WeatherDesc = "晴",
            FetchTime = DateTime.Now
        };
        await File.WriteAllTextAsync(_cachePath, JsonSerializer.Serialize(cachedInfo));
        using var service = CreateWeatherService(settings);

        var result = await service.GetWeatherAsync("广州", notifyUser: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetCityAsync_ConcurrentChanges_SerializesDelayedWritesAndKeepsLatestCity()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("WeatherApiKey", "test-key");
        settings.SetValue("WeatherApiHost", "test.qweatherapi.com");
        var location = new OverlappingLocationProvider();
        using var client = new HttpClient(new ThrowingHttpHandler());
        using var apiClient = new QWeatherApiClient(settings, client);
        using var service = new WeatherService(
            settings,
            new NoOpNotificationService(),
            location,
            apiClient,
            _cachePath);

        await service.SetCityAsync("上海");
        var oldRefresh = service.RefreshWeatherAsync(notifyUser: false);
        await location.WaitForCallAsync(1);

        var firstWriteStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstWriteRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriteStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        settings.SetSettingGate = async value =>
        {
            if (value == "北京")
            {
                firstWriteStarted.TrySetResult(true);
                await firstWriteRelease.Task;
            }
            else if (value == "广州")
            {
                secondWriteStarted.TrySetResult(true);
            }
        };
        settings.FailValue = "广州";

        var firstChange = service.SetCityAsync("北京");
        await firstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await location.FirstCallCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondChange = service.SetCityAsync("广州");

        var secondStartedBeforeFirstCompleted =
            await Task.WhenAny(secondWriteStarted.Task, Task.Delay(TimeSpan.FromMilliseconds(200))) == secondWriteStarted.Task;
        firstWriteRelease.TrySetResult(true);

        await firstChange;
        var secondFailure = await Record.ExceptionAsync(() => secondChange);
        await oldRefresh;

        Assert.False(secondStartedBeforeFirstCompleted);
        Assert.IsType<InvalidOperationException>(secondFailure);
        Assert.Equal("北京", settings.GetValue("City", ""));
    }

    [Fact]
    public async Task GetWeatherAsync_WhenHttpTransportFails_ShouldKeepNetworkUnavailableReason()
    {
        using var testLogs = new TestLogDirectoryScope();
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

    [Fact]
    public async Task RefreshWeatherAsync_WhenSupersededRefreshCompletes_DoesNotDisposeCurrentCancellationSource()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("WeatherApiKey", "test-key");
        settings.SetValue("WeatherApiHost", "test.qweatherapi.com");
        var location = new OverlappingLocationProvider();
        using var client = new HttpClient(new ThrowingHttpHandler());
        using var apiClient = new QWeatherApiClient(settings, client);
        using var service = new WeatherService(
            settings,
            new NoOpNotificationService(),
            location,
            apiClient,
            _cachePath);

        var firstRefresh = service.RefreshWeatherAsync(notifyUser: false);
        await location.WaitForCallAsync(1);

        using var secondExternalCancellation = new CancellationTokenSource();
        var secondRefresh = service.RefreshWeatherAsync(
            secondExternalCancellation.Token,
            notifyUser: false);
        await location.WaitForCallAsync(2);

        await firstRefresh;

        service.CancelRefresh();

        try
        {
            var completed = await Task.WhenAny(
                location.SecondCallCancelled.Task,
                Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(location.SecondCallCancelled.Task, completed);
        }
        finally
        {
            secondExternalCancellation.Cancel();
            await secondRefresh;
        }
    }

    [Fact]
    public async Task GetWeatherAsync_WhenCityChangesBeforeLateResponse_DoesNotCommitOldCity()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("WeatherApiKey", "test-key");
        settings.SetValue("WeatherApiHost", "test.qweatherapi.com");
        using var handler = new LateWeatherResponseHandler();
        using var client = new HttpClient(handler);
        using var apiClient = new QWeatherApiClient(settings, client);
        using var service = new WeatherService(
            settings,
            new NoOpNotificationService(),
            new StubLocationProvider(),
            apiClient,
            _cachePath);

        var oldCityRequest = service.GetWeatherAsync("北京", notifyUser: false);
        await handler.WaitForAirRequestAsync();

        await service.SetCityAsync("广州");
        handler.ReleaseAirResponse();

        var result = await oldCityRequest;

        Assert.Null(result);
        Assert.Null(await service.GetCachedWeatherAsync());
        Assert.False(File.Exists(_cachePath));
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
        public bool FailWrites { get; set; }
        public string? FailValue { get; set; }
        public Func<string?, Task>? SetSettingGate { get; set; }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<string?> GetSettingAsync(string key) => Task.FromResult(GetSetting(key));

        public string? GetSetting(string key) => _values.TryGetValue(key, out var value) ? value : null;

        public async Task SetSettingAsync(string key, string? value)
        {
            if (SetSettingGate != null)
            {
                await SetSettingGate(value);
            }

            if (FailWrites || string.Equals(FailValue, value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("forced settings failure");
            }

            SetSetting(key, value);
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

        public Task ReloadCacheAsync() => Task.CompletedTask;

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

    private sealed class OverlappingLocationProvider : ILocationProvider
    {
        private readonly TaskCompletionSource<bool> _firstCallStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _secondCallStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> SecondCallCancelled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> FirstCallCancelled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public LocationFailureReason LastFailure => LocationFailureReason.WindowsLocationUnavailable;

        public Task<(double Latitude, double Longitude)?> GetLocationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<(double, double)?>(null);

        public Task<string?> GetCityByCoordinatesAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public async Task<string?> ResolveCityAsync(CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                _firstCallStarted.TrySetResult(true);
            }
            else if (call == 2)
            {
                _secondCallStarted.TrySetResult(true);
            }

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            }
            catch (OperationCanceledException) when (call == 1)
            {
                FirstCallCancelled.TrySetResult(true);
                throw;
            }
            catch (OperationCanceledException) when (call == 2)
            {
                SecondCallCancelled.TrySetResult(true);
                throw;
            }
        }

        public Task WaitForCallAsync(int call) =>
            (call == 1 ? _firstCallStarted.Task : _secondCallStarted.Task)
                .WaitAsync(TimeSpan.FromSeconds(2));
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

    private sealed class LateWeatherResponseHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> _airRequestStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseAirResponse =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForAirRequestAsync() =>
            _airRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        public void ReleaseAirResponse() => _releaseAirResponse.TrySetResult(true);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("/airquality/", StringComparison.Ordinal))
            {
                _airRequestStarted.TrySetResult(true);
                await _releaseAirResponse.Task;
                return JsonResponse("{\"metadata\":{\"tag\":\"test\"},\"indexes\":[]}");
            }

            if (path.Contains("/geo/", StringComparison.Ordinal))
            {
                return JsonResponse("{\"code\":\"200\",\"location\":[{\"id\":\"101010100\",\"lat\":\"39.9\",\"lon\":\"116.4\"}]}");
            }

            if (path.EndsWith("/weather/now", StringComparison.Ordinal))
            {
                return JsonResponse("{\"code\":\"200\",\"now\":{\"temp\":\"25\",\"text\":\"晴\",\"icon\":\"100\",\"humidity\":\"40\"}}");
            }

            return JsonResponse("{\"code\":\"200\",\"daily\":[{\"tempMax\":\"30\",\"tempMin\":\"20\"}]}");
        }

        private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
    }
}
