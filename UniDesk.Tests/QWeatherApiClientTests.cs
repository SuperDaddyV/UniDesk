using System.Net;
using System.Net.Http;
using System.Text;
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

    [Fact]
    public async Task GetAsync_RedirectResponse_IsRejectedWithoutFollowingOrTreatingBodyAsSuccess()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("WeatherApiKey", "secret-key");
        settings.SetValue("WeatherApiHost", "abc.def.qweatherapi.com");
        using var handler = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://attacker.example/collect") },
            Content = new StringContent("{\"code\":\"200\"}", Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        using var client = new QWeatherApiClient(settings, httpClient);

        var result = await client.GetAsync(
            "/v7/weather/now",
            "location=101010100",
            legacyHost: "devapi.qweather.com",
            legacyPath: "/v7/weather/now");

        Assert.Null(result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("abc.def.qweatherapi.com", request.RequestUri!.Host);
        Assert.Equal("secret-key", Assert.Single(request.Headers.GetValues("X-QW-Api-Key")));
    }

    [Fact]
    public async Task GetAsync_ResponseBodyLargerThanOneMiB_IsRejected()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("WeatherApiKey", "secret-key");
        settings.SetValue("WeatherApiHost", "abc.def.qweatherapi.com");
        var body = "{\"code\":\"200\",\"payload\":\"" + new string('x', 1024 * 1024) + "\"}";
        using var handler = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        using var client = new QWeatherApiClient(settings, httpClient);

        var result = await client.GetAsync(
            "/v7/weather/now",
            "location=101010100");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_UnknownLengthResponseBodyLargerThanOneMiB_IsRejected()
    {
        var settings = new InMemorySettingsService();
        settings.SetValue("WeatherApiKey", "secret-key");
        settings.SetValue("WeatherApiHost", "abc.def.qweatherapi.com");
        var body = Encoding.UTF8.GetBytes("{\"code\":\"200\",\"payload\":\"" + new string('x', 1024 * 1024) + "\"}");
        using var handler = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(body)
        });
        using var httpClient = new HttpClient(handler);
        using var client = new QWeatherApiClient(settings, httpClient);

        var result = await client.GetAsync(
            "/v7/weather/now",
            "location=101010100");

        Assert.Null(result);
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responseFactory(request));
        }
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _content;

        public UnknownLengthContent(byte[] content)
        {
            _content = content;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new MemoryStream(_content, writable: false));
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
        public Task ReloadCacheAsync() => Task.CompletedTask;
        public Task FlushPendingSavesAsync() => Task.CompletedTask;
        public Task SaveBatchAsync(IReadOnlyDictionary<string, string?> values)
        {
            foreach (var (key, value) in values) _values[key] = value;
            return Task.CompletedTask;
        }
    }
}
