using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Helpers;

namespace UniDesk.Services;

/// <summary>
/// 和风天气 HTTP 客户端：用户凭据只发送到已校验的个人 API Host；内置凭据仍可兼容旧版公共域名。
/// </summary>
public class QWeatherApiClient : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;

    public QWeatherApiClient(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public string GetUserApiKey() => _settingsService.GetValue("WeatherApiKey", "").Trim();

    public string GetUserApiHost()
    {
        var rawHost = _settingsService.GetValue("WeatherApiHost", "");
        return TryNormalizeHost(rawHost, out var normalized) ? normalized : string.Empty;
    }

    public bool IsUsingBuiltInDefaults =>
        string.IsNullOrEmpty(GetUserApiKey()) &&
        string.IsNullOrWhiteSpace(_settingsService.GetValue("WeatherApiHost", ""));

    public string GetApiKey() => ResolveCredentials().ApiKey;

    public string GetApiHost() => ResolveCredentials().ApiHost;

    private (string ApiKey, string ApiHost) ResolveCredentials()
    {
        var userKey = GetUserApiKey();
        var rawUserHost = _settingsService.GetValue("WeatherApiHost", "").Trim();
        if (string.IsNullOrEmpty(userKey) && string.IsNullOrEmpty(rawUserHost))
        {
            var builtInHost = WeatherApiDefaults.GetDefaultApiHost(_settingsService);
            return TryNormalizeHost(builtInHost, out var normalizedBuiltInHost)
                ? (WeatherApiDefaults.GetDefaultApiKey(_settingsService), normalizedBuiltInHost)
                : (string.Empty, string.Empty);
        }

        if (string.IsNullOrEmpty(userKey) ||
            string.IsNullOrEmpty(rawUserHost) ||
            !TryNormalizeHost(rawUserHost, out var normalizedUserHost))
        {
            return (string.Empty, string.Empty);
        }

        return (userKey, normalizedUserHost);
    }

    public async Task<string?> GetAsync(
        string pathOnCustomHost,
        string query,
        CancellationToken cancellationToken = default,
        string? legacyHost = null,
        string? legacyPath = null)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        var host = GetApiHost();
        if (!string.IsNullOrEmpty(host))
        {
            var customResult = await SendAsync(host, pathOnCustomHost, query, apiKey, cancellationToken);
            if (customResult != null)
            {
                return customResult;
            }
        }

        if (IsUsingBuiltInDefaults &&
            !string.IsNullOrEmpty(legacyHost) &&
            !string.IsNullOrEmpty(legacyPath))
        {
            return await SendAsync(legacyHost, legacyPath, query, apiKey, cancellationToken, allowQueryKeyFallback: true);
        }

        return null;
    }

    public async Task<QWeatherValidationResult> ValidateAsync(
        string? apiKey = null,
        string? apiHost = null,
        CancellationToken cancellationToken = default)
    {
        apiKey = (apiKey ?? GetApiKey()).Trim();
        var rawHost = apiHost ?? GetApiHost();

        if (string.IsNullOrEmpty(apiKey))
        {
            return QWeatherValidationResult.Fail("API Key 为空");
        }

        if (string.IsNullOrWhiteSpace(rawHost))
        {
            return QWeatherValidationResult.Fail(
                "请填写 API Host（登录和风控制台查看，形如 xxx.qweatherapi.com），并确保与 API Key 属于同一项目。");
        }

        if (!TryNormalizeHost(rawHost, out apiHost))
        {
            return QWeatherValidationResult.Fail(
                "API Host 无效。仅允许和风控制台提供的 HTTPS 专属 qweatherapi.com 子域名。");
        }

        if (!string.IsNullOrEmpty(apiHost))
        {
            var response = await SendAsync(apiHost, "/v7/weather/now", "location=101010100", apiKey, cancellationToken);
            var code = ParseCode(response);
            if (code == "200")
            {
                return QWeatherValidationResult.Ok();
            }

            if (code == "401" || code == "403")
            {
                return QWeatherValidationResult.Fail(
                    "API Key 与 API Host 不匹配或凭据无效。请确认控制台中 Key 与 Host 属于同一项目。");
            }

            if (!string.IsNullOrEmpty(code))
            {
                return QWeatherValidationResult.Fail($"校验失败（和风错误码 {code}）");
            }
        }

        return QWeatherValidationResult.Fail(
            "无法连接和风天气服务，请检查网络或 API Host 是否正确。");
    }

    private async Task<string?> SendAsync(
        string host,
        string path,
        string query,
        string apiKey,
        CancellationToken cancellationToken,
        bool allowQueryKeyFallback = false)
    {
        var pathPart = path.StartsWith('/') ? path : "/" + path;
        var queryPart = string.IsNullOrEmpty(query) ? "" : (query.StartsWith('?') ? query : "?" + query);
        var url = $"https://{host}{pathPart}{queryPart}";

        var headerResult = await SendRequestAsync(url, apiKey, useHeaderAuth: true, cancellationToken);
        if (ParseCode(headerResult) == "200")
        {
            return headerResult;
        }

        if (!allowQueryKeyFallback)
        {
            return headerResult;
        }

        var urlWithKey = queryPart.Contains("key=")
            ? url
            : $"{url}{(queryPart.Contains('?') ? "&" : "?")}key={Uri.EscapeDataString(apiKey)}";
        var queryResult = await SendRequestAsync(urlWithKey, apiKey, useHeaderAuth: false, cancellationToken);
        if (ParseCode(queryResult) == "200")
        {
            return queryResult;
        }

        return queryResult ?? headerResult;
    }

    private async Task<string?> SendRequestAsync(
        string url,
        string apiKey,
        bool useHeaderAuth,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (useHeaderAuth)
            {
                request.Headers.TryAddWithoutValidation("X-QW-Api-Key", apiKey);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return body;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static string? ParseCode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<QWeatherCodeResponse>(json);
            return doc?.Code;
        }
        catch
        {
            return null;
        }
    }

    public static bool TryNormalizeHost(string? host, out string normalizedHost)
    {
        normalizedHost = string.Empty;
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        host = host.Trim();
        if (host.Contains("://", StringComparison.Ordinal) &&
            !host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? host
            : "https://" + host;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (!uri.IsDefaultPort && uri.Port != 443) ||
            (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) ||
            (uri.AbsolutePath != "/" && !string.IsNullOrEmpty(uri.AbsolutePath)))
        {
            return false;
        }

        var dnsHost = uri.IdnHost.ToLowerInvariant();
        if (IPAddress.TryParse(dnsHost, out _) ||
            !dnsHost.EndsWith(".qweatherapi.com", StringComparison.Ordinal) ||
            dnsHost.Length <= ".qweatherapi.com".Length)
        {
            return false;
        }

        normalizedHost = dnsHost;
        return true;
    }

    public static string NormalizeHost(string host) =>
        TryNormalizeHost(host, out var normalized)
            ? normalized
            : throw new FormatException("Invalid QWeather API Host.");

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private class QWeatherCodeResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}

public readonly struct QWeatherValidationResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; }

    public static QWeatherValidationResult Ok() => new() { IsValid = true, Message = string.Empty };

    public static QWeatherValidationResult Fail(string message) => new() { IsValid = false, Message = message };
}
