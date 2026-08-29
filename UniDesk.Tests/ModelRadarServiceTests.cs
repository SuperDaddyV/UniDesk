using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public sealed class ModelRadarServiceTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "UniDesk.ModelRadarServiceTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RefreshAsync_ShouldPreserveOverallOrder_SelectLeaderAndFirstValueTag()
    {
        var root = CreateDocument(
            new[]
            {
                Entry("leader", "Model A", "max", overall: 70, backend: 60, frontend: 80, knowledge: 70, tags: ["lowest_cost"]),
                Entry("value-first", "Model B", "high", overall: 95, backend: 95, frontend: 90, knowledge: 90, tags: ["value"]),
                Entry("value-second", "Model C", "medium", overall: 80, backend: 75, frontend: 70, knowledge: 85, tags: ["value"])
            });

        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);
        var service = CreateService(client);

        var result = await service.RefreshAsync(CancellationToken.None);

        AssertStatus(result, "Success");
        Assert.NotNull(result.Snapshot);
        var snapshot = result.Snapshot!;
        Assert.False(snapshot.IsPending);
        Assert.Equal("overall-1", snapshot.BatchId);
        Assert.Equal("leader", snapshot.OverallLeader!.Id);
        Assert.Equal(
            ["leader", "value-first", "value-second"],
            snapshot.OverallTop.Select(item => item.Entry.Id));
        Assert.Equal(["lowest_cost"], snapshot.OverallTop[0].Entry.DecisionTags);
        Assert.Equal("value-first", snapshot.ValueRecommendation!.Id);
        Assert.Equal(95d, snapshot.ValueRecommendation.OverallScore);
    }

    [Fact]
    public async Task RefreshAsync_WithoutValueTag_ShouldNotInventValueRecommendation()
    {
        var root = CreateDocument(
            [
                Entry(
                    "lowest-cost",
                    "Lowest Cost Model",
                    "low",
                    overall: 75,
                    backend: 75,
                    frontend: 75,
                    knowledge: 75,
                    tags: ["lowest_cost"])
            ]);
        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);

        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "Success");
        Assert.Null(result.Snapshot!.ValueRecommendation);
    }

    [Fact]
    public async Task RefreshAsync_ShouldSortDerivedListsStablyAndPutMissingScoresLast()
    {
        var root = CreateDocument(
            new[]
            {
                Entry("a", "Model A", "max", overall: 80, backend: 80, frontend: 50, knowledge: 50, rank: 1),
                Entry("b", "Model B", "high", overall: 79, backend: 80, frontend: 70, knowledge: 50, rank: 2),
                Entry("c", "Model C", "medium", overall: 78, backend: null, frontend: 70, knowledge: 40, rank: 3),
                Entry("d", "Model D", "low", overall: 77, backend: 70, frontend: null, knowledge: null, rank: 4)
            });

        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);
        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "Success");
        Assert.NotNull(result.Snapshot);
        var snapshot = result.Snapshot!;
        Assert.Equal(["a", "b", "d", "c"], snapshot.BackendTop.Select(item => item.Entry.Id));
        Assert.Equal(["b", "c", "a", "d"], snapshot.FrontendTop.Select(item => item.Entry.Id));
        Assert.Equal(["a", "b", "c", "d"], snapshot.KnowledgeTop.Select(item => item.Entry.Id));
        Assert.Equal([80d, 80d, 70d, null], snapshot.BackendTop.Select(item => item.Score));
        Assert.Equal([70d, 70d, 50d, null], snapshot.FrontendTop.Select(item => item.Score));
        Assert.Equal([50d, 50d, 40d, null], snapshot.KnowledgeTop.Select(item => item.Score));
        Assert.Equal([1, 2, 3, 4], snapshot.BackendTop.Select(item => item.Position));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefreshAsync_ShouldUseBackendRankingsWhenOverallIsPending(bool overallBatchIsNull)
    {
        var root = CreateDocument(
            new[]
            {
                Entry("backend-one", "Backend One", "high", overall: null, backend: 90, frontend: null, knowledge: null, rank: 1, score: 90, tags: ["value"]),
                Entry("backend-two", "Backend Two", "medium", overall: null, backend: 80, frontend: null, knowledge: null, rank: 2, score: 80)
            },
            includeOverallRankings: overallBatchIsNull,
            overallBatchIsNull: overallBatchIsNull);

        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);
        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "Success");
        Assert.NotNull(result.Snapshot);
        var snapshot = result.Snapshot!;
        Assert.True(snapshot.IsPending);
        Assert.Empty(snapshot.OverallTop);
        Assert.Empty(snapshot.FrontendTop);
        Assert.Empty(snapshot.KnowledgeTop);
        Assert.Equal(["backend-one", "backend-two"], snapshot.BackendTop.Select(item => item.Entry.Id));
        Assert.Equal([90d, 80d], snapshot.BackendTop.Select(item => item.Score));
        Assert.Equal("backend-one", snapshot.ValueRecommendation!.Id);
    }

    [Fact]
    public async Task RefreshAsync_ShouldAcceptUnknownFields()
    {
        var root = CreateDocument(
            [Entry("known", "Known Model", "high", overall: 80, backend: 80, frontend: 80, knowledge: 80, rank: 1)]);
        root["futureField"] = new JsonObject { ["version"] = 2 };
        ((JsonArray)root["overallRankings"]!)[0]!.AsObject()["futureEntryField"] = "ignored";

        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);
        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "Success");
        Assert.NotNull(result.Snapshot);
        Assert.Equal("known", result.Snapshot!.OverallLeader!.Id);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("schema-format")]
    [InlineData("license")]
    [InlineData("rank")]
    [InlineData("id")]
    [InlineData("model")]
    [InlineData("reasoning")]
    [InlineData("score")]
    [InlineData("date")]
    [InlineData("structure")]
    public async Task RefreshAsync_ShouldReturnSchemaErrorForInvalidKeyStructure(string invalidPart)
    {
        var root = CreateDocument(
            [Entry("known", "Known Model", "high", overall: 80, backend: 80, frontend: 80, knowledge: 80, rank: 1)]);

        switch (invalidPart)
        {
            case "schema":
                root["schemaVersion"] = "2.0";
                break;
            case "schema-format":
                root["schemaVersion"] = "1";
                break;
            case "license":
                root["source"]!.AsObject()["license"] = "Proprietary";
                break;
            case "rank":
                ((JsonArray)root["overallRankings"]!)[0]!.AsObject()["rank"] = 0;
                break;
            case "id":
                ((JsonArray)root["overallRankings"]!)[0]!.AsObject()["id"] = "";
                break;
            case "model":
                ((JsonArray)root["overallRankings"]!)[0]!.AsObject()["model"] = " ";
                break;
            case "reasoning":
                ((JsonArray)root["overallRankings"]!)[0]!.AsObject()["reasoningEffort"] = "";
                break;
            case "score":
                ((JsonArray)root["overallRankings"]!)[0]!.AsObject()["backendScore"] = 100.1;
                break;
            case "date":
                root["batch"]!.AsObject()["publishedAt"] = "not-a-date";
                root["overallBatch"]!.AsObject()["publishedAt"] = "not-a-date";
                break;
            case "structure":
                root["overallRankings"] = new JsonObject { ["not"] = "an array" };
                break;
        }

        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);
        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "SchemaError");
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_ShouldPreserveZeroAndNullNumericValues()
    {
        var root = CreateDocument(
            [
                Entry("zero", "Zero Model", "default", overall: 0, backend: 0, frontend: 0, knowledge: 0, rank: 1, elapsed: 0, cost: 0),
                Entry("missing", "Missing Model", "high", overall: null, backend: null, frontend: null, knowledge: null, rank: 2, elapsed: null, cost: null)
            ]);

        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);
        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "Success");
        Assert.NotNull(result.Snapshot);
        var snapshot = result.Snapshot!;
        var zero = snapshot.OverallTop.Single(item => item.Entry.Id == "zero").Entry;
        var missing = snapshot.OverallTop.Single(item => item.Entry.Id == "missing").Entry;
        Assert.Equal(0d, zero.OverallScore);
        Assert.Equal(0d, zero.BackendScore);
        Assert.Equal(0d, zero.FrontendScore);
        Assert.Equal(0d, zero.KnowledgeScore);
        Assert.Equal(0d, Convert.ToDouble(zero.ElapsedMilliseconds));
        Assert.Equal(0d, Convert.ToDouble(zero.EstimatedReferenceCostUsd));
        Assert.Null(missing.OverallScore);
        Assert.Null(missing.BackendScore);
        Assert.Null(missing.FrontendScore);
        Assert.Null(missing.KnowledgeScore);
        Assert.Null(missing.ElapsedMilliseconds);
        Assert.Null(missing.EstimatedReferenceCostUsd);
    }

    [Fact]
    public async Task RefreshAsync_ShouldUseFixedEndpointAndUniDeskUserAgent()
    {
        var root = CreateDocument(
            [Entry("known", "Known Model", "high", overall: 80, backend: 80, frontend: 80, knowledge: 80, rank: 1)]);
        using var handler = new RecordingHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://modeldial.com/api/v1/radar/latest.json", request.RequestUri!.AbsoluteUri);
            Assert.StartsWith("UniDesk/", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
            return Task.FromResult(JsonResponse(root));
        });
        using var client = new HttpClient(handler);

        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "Success");
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshAsync_ShouldPropagateCancellation()
    {
        using var handler = new BlockingHandler();
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var refresh = CreateService(client).RefreshAsync(cancellation.Token);
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await refresh);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRejectResponsesOverOneMiB()
    {
        var body = new string('x', 1024 * 1024 + 1);
        using var handler = new RecordingHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }));
        using var client = new HttpClient(handler);

        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "ResponseTooLarge");
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnAlreadyRefreshingWithoutConcurrentRequest()
    {
        var root = CreateDocument(
            [Entry("known", "Known Model", "high", overall: 80, backend: 80, frontend: 80, knowledge: 80, rank: 1)]);
        using var handler = new BlockingHandler(JsonResponse(root));
        using var client = new HttpClient(handler);
        var service = CreateService(client);

        var first = service.RefreshAsync(CancellationToken.None);
        await handler.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await service.RefreshAsync(CancellationToken.None);

        AssertStatus(second, "AlreadyRefreshing");
        Assert.Equal(1, handler.RequestCount);
        handler.Release();
        AssertStatus(await first, "Success");
    }

    [Fact]
    public async Task RefreshAsync_ShouldPersistCacheWithFetchTimestampAndReadItImmediately()
    {
        var now = new DateTimeOffset(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);
        var cachePath = CachePath();
        var root = CreateDocument(
            [Entry("known", "Known Model", "high", overall: 80, backend: 80, frontend: 80, knowledge: 80, rank: 1)]);
        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);
        var first = CreateService(client, cachePath, now);

        var refreshed = await first.RefreshAsync(CancellationToken.None);

        AssertStatus(refreshed, "Success");
        Assert.True(refreshed.CachePersisted);
        Assert.True(File.Exists(cachePath));
        Assert.Equal(now, refreshed.CachedAtUtc);

        using var offlineHandler = new RecordingHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var offlineClient = new HttpClient(offlineHandler);
        var second = CreateService(offlineClient, cachePath, now.AddMinutes(1));
        var cached = await second.ReadCacheAsync(CancellationToken.None);

        AssertStatus(cached, "Success");
        Assert.Equal(now, cached.CachedAtUtc);
        Assert.Equal(0, offlineHandler.RequestCount);
        Assert.NotNull(cached.Snapshot);
        Assert.Equal("known", cached.Snapshot!.OverallLeader!.Id);
    }

    [Fact]
    public async Task ReadCacheAsync_ShouldIgnoreCorruptCacheWithoutDeletingIt()
    {
        var cachePath = CachePath();
        var bytes = Encoding.UTF8.GetBytes("{ definitely not valid json");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllBytesAsync(cachePath, bytes);
        using var client = new HttpClient(new RecordingHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await CreateService(client, cachePath).ReadCacheAsync(CancellationToken.None);

        AssertStatus(result, "InvalidCache");
        Assert.Null(result.Snapshot);
        Assert.True(File.Exists(cachePath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(cachePath));
    }

    [Fact]
    public async Task RefreshAsync_ShouldAtomicallyLeaveOnlyValidCacheFile()
    {
        var cachePath = CachePath();
        var root = CreateDocument(
            [Entry("known", "Known Model", "high", overall: 80, backend: 80, frontend: 80, knowledge: 80, rank: 1)]);
        using var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(root)));
        using var client = new HttpClient(handler);

        var result = await CreateService(client, cachePath).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "Success");
        Assert.True(result.CachePersisted);
        Assert.NotEmpty(await File.ReadAllTextAsync(cachePath));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.GetDirectoryName(cachePath)!),
            path => Path.GetFileName(path).StartsWith(
                "modeldial-radar.json.",
                StringComparison.Ordinal));
        Assert.DoesNotContain("definitely not valid", await File.ReadAllTextAsync(cachePath));
    }

    [Fact]
    public async Task RefreshAsync_ShouldMapNotFoundResponse()
    {
        using var handler = new RecordingHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var client = new HttpClient(handler);

        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "NotFound");
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_ShouldMapTransportFailure()
    {
        using var handler = new RecordingHandler(_ => throw new HttpRequestException("offline"));
        using var client = new HttpClient(handler);

        var result = await CreateService(client).RefreshAsync(CancellationToken.None);

        AssertStatus(result, "NetworkError");
        Assert.Null(result.Snapshot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private ModelRadarService CreateService(HttpClient client, string? cachePath = null, DateTimeOffset? now = null)
    {
        return new ModelRadarService(
            client,
            cachePath ?? CachePath(),
            now is null ? null : new FixedTimeProvider(now.Value));
    }

    private string CachePath()
    {
        return Path.Combine(_testDirectory, "cache", "modeldial-radar.json");
    }

    private static HttpResponseMessage JsonResponse(JsonObject root)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json")
        };
    }

    private static JsonObject CreateDocument(
        IReadOnlyList<JsonObject> entries,
        bool includeOverallRankings = true,
        bool overallBatchIsNull = false)
    {
        var rankings = new JsonArray(entries.Select(entry => JsonNode.Parse(entry.ToJsonString())!).ToArray());
        var root = new JsonObject
        {
            ["schemaVersion"] = "1.1",
            ["generatedAt"] = "2026-08-30T07:00:00Z",
            ["source"] = new JsonObject
            {
                ["name"] = "ModelDial Radar",
                ["license"] = "CC BY 4.0"
            },
            ["batch"] = new JsonObject
            {
                ["id"] = "batch-1",
                ["publishedAt"] = "2026-08-30T06:00:00Z"
            },
            ["overallBatch"] = overallBatchIsNull
                ? null
                : new JsonObject
                {
                    ["id"] = "overall-1",
                    ["publishedAt"] = "2026-08-30T07:00:00Z"
                },
            ["overallRankings"] = includeOverallRankings
                ? new JsonArray(entries.Select(entry => JsonNode.Parse(entry.ToJsonString())!).ToArray())
                : new JsonArray(),
            ["rankings"] = rankings
        };
        return root;
    }

    private static JsonObject Entry(
        string id,
        string model,
        string reasoning,
        double? overall,
        double? backend,
        double? frontend,
        double? knowledge,
        int? rank = null,
        double? score = null,
        double? elapsed = 123,
        double? cost = 0.25,
        string[]? tags = null)
    {
        return new JsonObject
        {
            ["rank"] = rank ?? (id == "leader" ? 1 : id == "value-first" ? 2 : id == "value-second" ? 3 : 1),
            ["id"] = id,
            ["model"] = model,
            ["displayName"] = $"{model} / {reasoning}",
            ["reasoningEffort"] = reasoning,
            ["route"] = "official_login",
            ["score"] = score ?? overall,
            ["overallScore"] = overall,
            ["backendScore"] = backend,
            ["frontendScore"] = frontend,
            ["knowledgeScore"] = knowledge,
            ["elapsedMs"] = elapsed,
            ["estimatedReferenceCostUsd"] = cost,
            ["decisionTags"] = tags is null ? new JsonArray() : new JsonArray(tags.Select(tag => JsonValue.Create(tag)).ToArray())
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return responder(request);
        }
    }

    private sealed class BlockingHandler(HttpResponseMessage? response = null) : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        public TaskCompletionSource<bool> RequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RequestCount => _requestCount;

        public void Release()
        {
            _release.TrySetResult(response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            RequestStarted.TrySetResult(true);
            return await _release.Task.WaitAsync(cancellationToken);
        }
    }

    private static void AssertStatus(ModelRadarServiceResult result, string expected)
    {
        Assert.Equal(expected, result.Status.ToString());
    }
}
