using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed class GitHubUpdateService : IUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/SuperDaddyV/UniDesk/releases/latest";
    private const string ReleasesUrl = "https://api.github.com/repos/SuperDaddyV/UniDesk/releases";
    private const string LatestReleasePageUrl = "https://github.com/SuperDaddyV/UniDesk/releases/latest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public GitHubUpdateService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("UniDesk");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public string CurrentVersion => AppVersionProvider.CurrentVersion;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await GetLatestStableReleaseAsync(cancellationToken);
            if (release == null)
            {
                return Failed("No stable GitHub release found.");
            }

            var latestVersion = NormalizeVersionTag(release.TagName);
            if (!TryParseVersion(CurrentVersion, out var current) ||
                !TryParseVersion(latestVersion, out var latest))
            {
                return Failed("Invalid version format.");
            }

            var comparison = current.CompareTo(latest);
            var status = comparison < 0
                ? UpdateCheckStatus.UpdateAvailable
                : comparison == 0
                    ? UpdateCheckStatus.Latest
                    : UpdateCheckStatus.CurrentNewerThanLatest;

            return new UpdateCheckResult
            {
                Status = status,
                CurrentVersion = WithVersionPrefix(CurrentVersion),
                LatestVersion = WithVersionPrefix(latestVersion),
                ReleaseName = string.IsNullOrWhiteSpace(release.Name) ? WithVersionPrefix(latestVersion) : release.Name,
                ReleaseNotes = release.Body ?? string.Empty,
                PublishedAt = release.PublishedAt,
                ReleaseUrl = string.IsNullOrWhiteSpace(release.HtmlUrl)
                    ? "https://github.com/SuperDaddyV/UniDesk/releases/latest"
                    : release.HtmlUrl,
                InstallerDownloadUrl = SelectInstallerAsset(release.Assets)
            };
        }
        catch (OperationCanceledException)
        {
            return Failed("Request timed out or was canceled.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return Failed("GitHub API rate limit or permission error.");
        }
        catch (Exception ex)
        {
            return Failed(ex.Message);
        }
    }

    public static int CompareVersionTags(string currentVersion, string latestVersion)
    {
        if (!TryParseVersion(NormalizeVersionTag(currentVersion), out var current) ||
            !TryParseVersion(NormalizeVersionTag(latestVersion), out var latest))
        {
            throw new ArgumentException("Invalid semantic version.");
        }

        return current.CompareTo(latest);
    }

    private async Task<GitHubReleaseDto?> GetLatestStableReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var latest = await ReadReleaseAsync(LatestReleaseUrl, cancellationToken);
            if (latest is { Draft: false, Prerelease: false })
            {
                return latest;
            }

            var releases = await ReadReleaseListAsync(ReleasesUrl, cancellationToken);
            return releases.FirstOrDefault(release => !release.Draft && !release.Prerelease);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return await ReadLatestReleaseRedirectAsync(cancellationToken);
        }
    }

    private async Task<GitHubReleaseDto?> ReadReleaseAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, JsonOptions, cancellationToken);
    }

    private async Task<List<GitHubReleaseDto>> ReadReleaseListAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<List<GitHubReleaseDto>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private async Task<GitHubReleaseDto?> ReadLatestReleaseRedirectAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleasePageUrl);
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var releaseUrl = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;
        var tagName = ExtractTagNameFromReleaseUrl(releaseUrl);
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        return new GitHubReleaseDto
        {
            TagName = tagName,
            Name = WithVersionPrefix(NormalizeVersionTag(tagName)),
            HtmlUrl = releaseUrl,
            Body = string.Empty,
            Draft = false,
            Prerelease = false,
            Assets = []
        };
    }

    private static UpdateCheckResult Failed(string message) => new()
    {
        Status = UpdateCheckStatus.Failed,
        CurrentVersion = WithVersionPrefix(AppVersionProvider.CurrentVersion),
        ErrorMessage = message
    };

    private static string NormalizeVersionTag(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var normalized = version.Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOf('+');
        return metadataIndex >= 0 ? normalized[..metadataIndex] : normalized;
    }

    private static string WithVersionPrefix(string version) =>
        version.StartsWith('v') || version.StartsWith('V') ? version : "v" + version;

    private static string ExtractTagNameFromReleaseUrl(string releaseUrl)
    {
        if (string.IsNullOrWhiteSpace(releaseUrl) ||
            !Uri.TryCreate(releaseUrl, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (string.Equals(segments[i], "tag", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(segments[i + 1]);
            }
        }

        return string.Empty;
    }

    private static bool TryParseVersion(string version, out Version parsed)
    {
        parsed = new Version(0, 0, 0);
        var normalized = NormalizeVersionTag(version);
        var dashIndex = normalized.IndexOf('-');
        if (dashIndex >= 0)
        {
            normalized = normalized[..dashIndex];
        }

        return Version.TryParse(normalized, out parsed!);
    }

    private static string? SelectInstallerAsset(IEnumerable<GitHubAssetDto>? assets)
    {
        if (assets == null)
        {
            return null;
        }

        var candidates = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            .Select(asset => new
            {
                Asset = asset,
                Name = asset.Name ?? string.Empty
            })
            .Where(item =>
                item.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                item.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => ContainsAny(item.Name, "UniDesk", "Setup", "Installer"))
            .FirstOrDefault();

        return candidates?.Asset.BrowserDownloadUrl;
    }

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto>? Assets { get; init; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}
