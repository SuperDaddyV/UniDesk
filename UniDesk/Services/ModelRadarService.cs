using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed class ModelRadarService : IModelRadarService, IDisposable
{
    private const int MaximumResponseBytes = 1024 * 1024;
    private const int MaximumCacheBytes = MaximumResponseBytes + (64 * 1024);
    private static readonly Uri LatestRadarUri = new(
        "https://modeldial.com/api/v1/radar/latest.json",
        UriKind.Absolute);

    private readonly HttpClient _httpClient;
    private readonly string _cachePath;
    private readonly TimeProvider _timeProvider;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private bool _disposed;

    public ModelRadarService()
        : this(
            CreateHttpClient(),
            Path.Combine(DirectoryHelper.CacheDirectory, "modeldial-radar.json"),
            TimeProvider.System,
            ownsHttpClient: true)
    {
    }

    internal ModelRadarService(
        HttpClient httpClient,
        string cachePath,
        TimeProvider? timeProvider = null,
        bool ownsHttpClient = false)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);

        _httpClient = httpClient;
        _cachePath = cachePath;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _ownsHttpClient = ownsHttpClient;
    }

    public async Task<ModelRadarServiceResult> ReadCacheAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!File.Exists(_cachePath))
        {
            return Result(ModelRadarServiceStatus.NotFound);
        }

        try
        {
            var fileInfo = new FileInfo(_cachePath);
            if (fileInfo.Length <= 0 || fileInfo.Length > MaximumCacheBytes)
            {
                return Result(ModelRadarServiceStatus.InvalidCache);
            }

            var bytes = await ReadBoundedAsync(
                new FileStream(
                    _cachePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan),
                MaximumCacheBytes,
                cancellationToken);
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetInt32(root, "cacheVersion", out var cacheVersion) ||
                cacheVersion != 1 ||
                !TryGetDateTimeOffset(root, "cachedAtUtc", out var cachedAtUtc) ||
                !root.TryGetProperty("payload", out var payload))
            {
                return Result(ModelRadarServiceStatus.InvalidCache);
            }

            var snapshot = ParseSnapshot(payload);
            return new ModelRadarServiceResult
            {
                Status = ModelRadarServiceStatus.Success,
                Snapshot = snapshot,
                CachedAtUtc = cachedAtUtc,
                CachePersisted = true
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or JsonException or SchemaException or ResponseTooLargeException)
        {
            return Result(ModelRadarServiceStatus.InvalidCache);
        }
    }

    public async Task<ModelRadarServiceResult> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!await _refreshGate.WaitAsync(0, cancellationToken))
        {
            return Result(ModelRadarServiceStatus.AlreadyRefreshing);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestRadarUri);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                $"UniDesk/{AppVersionProvider.CurrentVersion}");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result(ModelRadarServiceStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result(ModelRadarServiceStatus.NetworkError);
            }

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaximumResponseBytes)
            {
                return Result(ModelRadarServiceStatus.ResponseTooLarge);
            }

            var payload = await ReadBoundedAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                MaximumResponseBytes,
                cancellationToken);

            using var document = JsonDocument.Parse(payload);
            var snapshot = ParseSnapshot(document.RootElement);
            var cachedAtUtc = _timeProvider.GetUtcNow();
            var cachePersisted = await TryWriteCacheAsync(
                document.RootElement,
                cachedAtUtc,
                cancellationToken);

            return new ModelRadarServiceResult
            {
                Status = ModelRadarServiceStatus.Success,
                Snapshot = snapshot,
                CachedAtUtc = cachedAtUtc,
                CachePersisted = cachePersisted
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Result(ModelRadarServiceStatus.NetworkError);
        }
        catch (ResponseTooLargeException)
        {
            return Result(ModelRadarServiceStatus.ResponseTooLarge);
        }
        catch (SchemaException)
        {
            return Result(ModelRadarServiceStatus.SchemaError);
        }
        catch (JsonException)
        {
            return Result(ModelRadarServiceStatus.SchemaError);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            return Result(ModelRadarServiceStatus.NetworkError);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip |
                                     DecompressionMethods.Deflate |
                                     DecompressionMethods.Brotli,
            UseProxy = true
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using (stream)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                using var output = new MemoryStream();
                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read == 0)
                    {
                        return output.ToArray();
                    }

                    if (output.Length + read > maximumBytes)
                    {
                        throw new ResponseTooLargeException();
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private static ModelRadarSnapshot ParseSnapshot(JsonElement root)
    {
        RequireKind(root, JsonValueKind.Object);

        var schemaVersion = RequireString(root, "schemaVersion");
        if (!schemaVersion.StartsWith("1.", StringComparison.Ordinal) ||
            !Version.TryParse(schemaVersion, out var version) ||
            version.Major != 1)
        {
            throw new SchemaException();
        }

        var source = RequireObject(root, "source");
        if (!string.Equals(
                RequireString(source, "license"),
                "CC BY 4.0",
                StringComparison.Ordinal))
        {
            throw new SchemaException();
        }

        var backendBatch = RequireObject(root, "batch");
        var backendBatchId = RequireString(backendBatch, "id");
        var backendPublishedAt = RequireDateTimeOffset(backendBatch, "publishedAt");
        var overallRankingsElement = RequireArray(root, "overallRankings");
        var backendRankingsElement = RequireArray(root, "rankings");

        var overallEntries = ParseEntries(overallRankingsElement);
        var backendEntries = ParseEntries(backendRankingsElement);
        var hasOverallBatch = root.TryGetProperty("overallBatch", out var overallBatch) &&
                              overallBatch.ValueKind == JsonValueKind.Object;
        if (root.TryGetProperty("overallBatch", out overallBatch) &&
            overallBatch.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
        {
            throw new SchemaException();
        }

        var isPending = !hasOverallBatch || overallEntries.Count == 0;
        if (isPending)
        {
            var backendTop = backendEntries
                .Take(5)
                .Select((entry, index) => new ModelRadarListItem(
                    index + 1,
                    entry,
                    entry.BackendScore))
                .ToArray();

            return new ModelRadarSnapshot
            {
                SchemaVersion = schemaVersion,
                BatchId = backendBatchId,
                PublishedAt = backendPublishedAt,
                IsPending = true,
                ValueRecommendation = backendEntries.FirstOrDefault(HasValueTag),
                BackendTop = backendTop
            };
        }

        var overallBatchId = RequireString(overallBatch, "id");
        var overallPublishedAt = RequireDateTimeOffset(overallBatch, "publishedAt");
        return new ModelRadarSnapshot
        {
            SchemaVersion = schemaVersion,
            BatchId = overallBatchId,
            PublishedAt = overallPublishedAt,
            OverallLeader = overallEntries[0],
            ValueRecommendation = overallEntries.FirstOrDefault(HasValueTag),
            OverallTop = CreatePublisherOrderTop(overallEntries, entry => entry.OverallScore),
            BackendTop = CreateDerivedTop(overallEntries, entry => entry.BackendScore),
            FrontendTop = CreateDerivedTop(overallEntries, entry => entry.FrontendScore),
            KnowledgeTop = CreateDerivedTop(overallEntries, entry => entry.KnowledgeScore)
        };
    }

    private static IReadOnlyList<ModelRadarEntry> ParseEntries(JsonElement array)
    {
        var entries = new List<ModelRadarEntry>(array.GetArrayLength());
        var position = 0;
        foreach (var element in array.EnumerateArray())
        {
            position++;
            RequireKind(element, JsonValueKind.Object);
            var rank = RequirePositiveInt32(element, "rank");
            var backendRank = TryGetOptionalPositiveInt32(element, "backendRank");
            var id = RequireString(element, "id");
            var model = RequireString(element, "model");
            var reasoningEffort = RequireString(element, "reasoningEffort");
            var displayName = TryGetOptionalString(element, "displayName") ??
                              $"{model} / {reasoningEffort}";
            var route = RequireString(element, "route");

            ValidateScore(element, "score");
            var overallScore = ReadScore(element, "overallScore");
            var backendScore = ReadScore(element, "backendScore");
            var frontendScore = ReadScore(element, "frontendScore");
            var knowledgeScore = ReadScore(element, "knowledgeScore");
            var elapsedMilliseconds = ReadOptionalNonNegativeInt64(element, "elapsedMs");
            var estimatedReferenceCostUsd = ReadOptionalNonNegativeDouble(
                element,
                "estimatedReferenceCostUsd");
            var decisionTags = ReadStringArray(element, "decisionTags");

            entries.Add(new ModelRadarEntry
            {
                PublishedPosition = position,
                Rank = rank,
                BackendRank = backendRank,
                Id = id,
                Model = model,
                DisplayName = displayName,
                ReasoningEffort = reasoningEffort,
                Route = route,
                OverallScore = overallScore,
                BackendScore = backendScore,
                FrontendScore = frontendScore,
                KnowledgeScore = knowledgeScore,
                ElapsedMilliseconds = elapsedMilliseconds,
                EstimatedReferenceCostUsd = estimatedReferenceCostUsd,
                DecisionTags = decisionTags
            });
        }

        return entries;
    }

    private static IReadOnlyList<ModelRadarListItem> CreatePublisherOrderTop(
        IReadOnlyList<ModelRadarEntry> entries,
        Func<ModelRadarEntry, double?> scoreSelector) =>
        entries
            .Take(5)
            .Select((entry, index) => new ModelRadarListItem(
                index + 1,
                entry,
                scoreSelector(entry)))
            .ToArray();

    private static IReadOnlyList<ModelRadarListItem> CreateDerivedTop(
        IReadOnlyList<ModelRadarEntry> entries,
        Func<ModelRadarEntry, double?> scoreSelector) =>
        entries
            .OrderBy(entry => scoreSelector(entry) is null)
            .ThenByDescending(entry => scoreSelector(entry))
            .ThenBy(entry => entry.PublishedPosition)
            .Take(5)
            .Select((entry, index) => new ModelRadarListItem(
                index + 1,
                entry,
                scoreSelector(entry)))
            .ToArray();

    private async Task<bool> TryWriteCacheAsync(
        JsonElement payload,
        DateTimeOffset cachedAtUtc,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_cachePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var tempPath = $"{_cachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                using var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();
                writer.WriteNumber("cacheVersion", 1);
                writer.WriteString("cachedAtUtc", cachedAtUtc);
                writer.WritePropertyName("payload");
                payload.WriteTo(writer);
                writer.WriteEndObject();
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_cachePath))
            {
                File.Replace(tempPath, _cachePath, destinationBackupFileName: null);
            }
            else
            {
                try
                {
                    File.Move(tempPath, _cachePath);
                }
                catch (IOException) when (File.Exists(_cachePath))
                {
                    File.Replace(tempPath, _cachePath, destinationBackupFileName: null);
                }
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.LogError(ex, "ModelRadarService.WriteCache");
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogWarning(
                    $"无法清理模型雷达临时缓存：{ex.Message}",
                    "ModelRadarService.WriteCache");
            }
        }
    }

    private static bool HasValueTag(ModelRadarEntry entry) =>
        entry.DecisionTags.Contains("value", StringComparer.Ordinal);

    private static JsonElement RequireObject(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new SchemaException();
        }

        return value;
    }

    private static JsonElement RequireArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new SchemaException();
        }

        return value;
    }

    private static string RequireString(JsonElement parent, string name) =>
        TryGetOptionalString(parent, name) ?? throw new SchemaException();

    private static string? TryGetOptionalString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new SchemaException();
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? throw new SchemaException() : text;
    }

    private static int RequirePositiveInt32(JsonElement parent, string name)
    {
        if (!TryGetInt32(parent, name, out var value) || value <= 0)
        {
            throw new SchemaException();
        }

        return value;
    }

    private static int TryGetOptionalPositiveInt32(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return 0;
        }

        if (!value.TryGetInt32(out var result) || result <= 0)
        {
            throw new SchemaException();
        }

        return result;
    }

    private static bool TryGetInt32(JsonElement parent, string name, out int value)
    {
        value = default;
        return parent.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt32(out value);
    }

    private static DateTimeOffset RequireDateTimeOffset(JsonElement parent, string name) =>
        TryGetDateTimeOffset(parent, name, out var value) ? value : throw new SchemaException();

    private static bool TryGetDateTimeOffset(
        JsonElement parent,
        string name,
        out DateTimeOffset value)
    {
        value = default;
        return parent.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(
                   element.GetString(),
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.RoundtripKind,
                   out value);
    }

    private static double? ReadScore(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetDouble(out var value) ||
            !double.IsFinite(value) ||
            value is < 0 or > 100)
        {
            throw new SchemaException();
        }

        return value;
    }

    private static void ValidateScore(JsonElement parent, string name)
    {
        _ = ReadScore(parent, name);
    }

    private static long? ReadOptionalNonNegativeInt64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt64(out var value) ||
            value < 0)
        {
            throw new SchemaException();
        }

        return value;
    }

    private static double? ReadOptionalNonNegativeDouble(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetDouble(out var value) ||
            !double.IsFinite(value) ||
            value < 0)
        {
            throw new SchemaException();
        }

        return value;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element))
        {
            return [];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new SchemaException();
        }

        var values = new List<string>(element.GetArrayLength());
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new SchemaException();
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static void RequireKind(JsonElement element, JsonValueKind expected)
    {
        if (element.ValueKind != expected)
        {
            throw new SchemaException();
        }
    }

    private static ModelRadarServiceResult Result(ModelRadarServiceStatus status) =>
        new() { Status = status };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed class SchemaException : Exception;
    private sealed class ResponseTooLargeException : Exception;
}
