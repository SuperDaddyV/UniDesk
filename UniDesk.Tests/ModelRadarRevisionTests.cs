using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public sealed class ModelRadarRevisionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "UniDesk.RadarRevisionTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PublicationV3_ShouldAcceptCurrentWeightsAndPreservePublishedScores()
    {
        var root = PublicationV3(Entry("a", "model-a", "max", 89.75, 81));
        using var service = Service(root);

        var result = await service.RefreshAsync();

        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        Assert.Equal(89.75, result.Snapshot!.OverallLeader!.OverallScore);
        Assert.Equal("five-axis-b20-f25-k35-a10-m10-cube-half_up-v3", result.Snapshot.PolicyId);
        Assert.Equal(25, result.Snapshot.ScoreWeights["frontend"]);
        Assert.Equal(10, result.Snapshot.ScoreWeights["aesthetic"]);
        Assert.False(result.Snapshot.IsOutdatedPolicy);
        Assert.True(result.CachePersisted);
    }

    [Fact]
    public async Task Publication_ShouldUseOfficialConfigurationTieBreaksAndCompetitionRanks()
    {
        var root = ModelRadarPublicationTests.Publication(
            Entry("a-max", "a", "max", 89.15, 95),
            Entry("a-xhigh", "a", "xhigh", 89.35, 95),
            Entry("b-max", "b", "max", 88.1, 95),
            Entry("c-high", "c", "high", 88, 94),
            Entry("c-low", "c", "low", 88, 94),
            Entry("d-z", "d", "high", 87, 93),
            Entry("d-a", "d", "high", 87, 93),
            Entry("e", "e", "high", 86, 92),
            Entry("f", "f", "high", 85, 91));
        using var service = Service(root);

        var result = await service.RefreshAsync();

        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        Assert.Equal(new[] { "a-xhigh", "b-max", "c-low", "d-a", "e" }, result.Snapshot!.BackendTop.Select(row => row.Entry.Id));
        Assert.Equal(new[] { 1, 1, 3, 4, 5 }, result.Snapshot.BackendTop.Select(row => row.Position));
    }

    [Fact]
    public async Task Publication_ShouldSortTiedModelsByOfficialNameBeforeTakingFive()
    {
        var root = PublicationV3(
            Entry("leader", "leader", "high", 80, 100),
            Entry("z", "first-raw-model", "high", 99, 95),
            Entry("e", "e", "high", 98, 95),
            Entry("d", "d", "high", 97, 95),
            Entry("c", "c", "high", 96, 95),
            Entry("b", "b", "high", 95, 95),
            Entry("a", "last-raw-model", "high", 94, 95));
        root["displayNames"] = new JsonObject
        {
            ["leader"] = "Leader", ["first-raw-model"] = "Zulu", ["e"] = "Echo",
            ["d"] = "Delta", ["c"] = "Charlie", ["b"] = "Bravo", ["last-raw-model"] = "Alpha"
        };
        using var service = Service(root);

        var result = await service.RefreshAsync();

        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        Assert.Equal(new[] { "leader", "a", "b", "c", "d" }, result.Snapshot!.BackendTop.Select(row => row.Entry.Id));
        Assert.Equal(new[] { 1, 2, 2, 2, 2 }, result.Snapshot.BackendTop.Select(row => row.Position));
    }

    [Fact]
    public async Task Publication_ShouldUseOfficialUnknownEffortOrderAndCultureBasedCandidateTieBreak()
    {
        var root = PublicationV3(
            Entry("a-low", "unknown-effort", "low", 80, 90),
            Entry("z-unknown", "unknown-effort", "unknown", 80, 90),
            Entry("Z-source", "candidate-order", "high", 80, 90),
            Entry("a-source", "candidate-order", "high", 80, 90));
        using var service = Service(root);

        var result = await service.RefreshAsync();

        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        Assert.Equal("z-unknown", result.Snapshot!.BackendTop.Single(row => row.Entry.Model == "unknown-effort").Entry.Id);
        Assert.Equal("a-source", result.Snapshot.BackendTop.Single(row => row.Entry.Model == "candidate-order").Entry.Id);
    }

    [Fact]
    public async Task PublicationV2_ShouldPreservePolicyAndMarkCacheAsOutdated()
    {
        using var service = Service(ModelRadarPublicationTests.Publication(Entry("a", "model-a", "max", 80, 80)));

        var result = await service.RefreshAsync();
        var cached = await service.ReadCacheAsync();

        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        Assert.Equal(ModelRadarServiceStatus.Success, cached.Status);
        Assert.True(result.Snapshot!.IsOutdatedPolicy);
        Assert.True(cached.Snapshot!.IsOutdatedPolicy);
        Assert.Equal("five-axis-b20-f20-k35-a15-m10-cube-half_up-v2", cached.Snapshot.PolicyId);
        Assert.Equal(20, cached.Snapshot.ScoreWeights["frontend"]);
        Assert.Equal(15, cached.Snapshot.ScoreWeights["aesthetic"]);
    }

    [Theory]
    [InlineData("policy")]
    [InlineData("weight")]
    public async Task PublicationV3_ShouldRejectUnknownPolicyOrMismatchedWeightsWithoutReplacingCache(string invalid)
    {
        var root = PublicationV3(Entry("a", "model-a", "max", 80, 80));
        using (var service = Service(root))
            Assert.Equal(ModelRadarServiceStatus.Success, (await service.RefreshAsync()).Status);
        var cache = Path.Combine(_directory, "cache.json");
        var original = await File.ReadAllBytesAsync(cache);
        if (invalid == "policy") root["qualityScores"]!["policy_id"] = "future-policy";
        else root["qualityScores"]!["weights"]!["frontend"] = 20;

        using var invalidService = Service(root);
        Assert.Equal(ModelRadarServiceStatus.SchemaError, (await invalidService.RefreshAsync()).Status);
        Assert.Equal(original, await File.ReadAllBytesAsync(cache));
    }

    [Fact]
    public async Task Publication_ShouldApplyEffortAliasesWithoutChangingIdentityOrTieBreaks()
    {
        var root = ModelRadarPublicationTests.Publication(
            Entry("a-max", "model-a", "max", 80, 90),
            Entry("a-xhigh", "model-a", "xhigh", 80, 90),
            Entry("b", "model-b", "unknown", 70, 80));
        root["effortDisplayNames"] = new JsonObject
        {
            ["model-a"] = new JsonObject { ["xhigh"] = "ultra" },
            ["model-b"] = new JsonObject { ["unknown"] = "high" }
        };
        using var service = Service(root);

        var result = await service.RefreshAsync();

        var selected = result.Snapshot!.BackendTop[0].Entry;
        Assert.Equal("a-xhigh", selected.Id);
        Assert.Equal("model-a", selected.Model);
        Assert.Equal("xhigh", selected.ReasoningEffort);
        Assert.Equal("ultra", selected.DisplayReasoningEffort);
        Assert.Equal("reference", selected.ConnectionId);
        Assert.Equal("high", result.Snapshot.BackendTop[1].Entry.DisplayReasoningEffort);
        Assert.Equal("unknown", result.Snapshot.BackendTop[1].Entry.ReasoningEffort);
    }

    [Fact]
    public async Task Publication_ShouldKeepEachCategoryMetricsAndOnlyCompleteCosts()
    {
        var root = ModelRadarPublicationTests.Publication(Entry("a", "model-a", "max", 80, 80));
        root["metrics"]!["a"] = new JsonObject
        {
            ["overall"] = Metrics(6000, 3, "complete"),
            ["backend"] = Metrics(1000, 1, "complete"),
            ["frontend"] = Metrics(2000, 2, "partial"),
            ["knowledge"] = Metrics(3000, 0, "complete")
        };
        using var service = Service(root);

        var result = await service.RefreshAsync();

        var entry = result.Snapshot!.OverallLeader!;
        Assert.Equal(new ModelRadarMetrics(6000, 3), entry.Metrics[ModelRadarCategory.Overall]);
        Assert.Equal(new ModelRadarMetrics(1000, 1), entry.Metrics[ModelRadarCategory.Backend]);
        Assert.Equal(new ModelRadarMetrics(2000, null), entry.Metrics[ModelRadarCategory.Frontend]);
        Assert.Equal(new ModelRadarMetrics(3000, 0), entry.Metrics[ModelRadarCategory.Knowledge]);
        Assert.False(entry.Metrics.ContainsKey(ModelRadarCategory.Aesthetic));
        Assert.False(entry.Metrics.ContainsKey(ModelRadarCategory.Maintainability));
        Assert.Equal(6000, entry.ElapsedMilliseconds);
        Assert.Equal(3, entry.EstimatedReferenceCostUsd);
    }

    [Fact]
    public async Task Publication_ShouldOnlyMarkModelsWithExplicitNewHighlight()
    {
        var recent = Entry("a", "a", "max", 90, 90);
        recent["decisionTags"] = new JsonArray("new");
        var root = ModelRadarPublicationTests.Publication(
            recent, Entry("b", "b", "max", 80, 80), Entry("c", "c", "max", 70, 70), Entry("d", "d", "max", 60, 60));
        root["highlights"] = new JsonObject
        {
            ["slots"] = new JsonArray(
                new JsonObject { ["model_id"] = "a", ["tags"] = new JsonArray("featured") },
                new JsonObject { ["model_id"] = "b", ["tags"] = new JsonArray("new", "featured") },
                new JsonObject { ["model_id"] = "c", ["tag"] = "new" })
        };
        using var service = Service(root);

        var result = await service.RefreshAsync();

        Assert.Equal(new[] { false, true, true, false }, result.Snapshot!.OverallTop.Select(row => row.Entry.IsRecentHighlight));
    }

    private static JsonObject Metrics(long elapsed, double cost, string coverage) => new()
    {
        ["elapsed_ms"] = elapsed, ["estimated_api_cost_usd"] = cost, ["cost_coverage"] = coverage
    };

    private ModelRadarService Service(JsonObject root) => new(
        new HttpClient(new PublicationHandler(root)), Path.Combine(_directory, "cache.json"), ownsHttpClient: true);

    private static JsonObject PublicationV3(params JsonObject[] entries)
    {
        var root = ModelRadarPublicationTests.Publication(entries);
        var quality = root["qualityScores"]!;
        quality["policy_id"] = "five-axis-b20-f25-k35-a10-m10-cube-half_up-v3";
        quality["weights"]!["frontend"] = 25;
        quality["weights"]!["aesthetic"] = 10;
        return root;
    }

    private static JsonObject Entry(string id, string model, string effort, double overall, double backend) =>
        ModelRadarPublicationTests.Entry(id, model, effort, overall, backend, 80, 80, 80, 80);

    private sealed class PublicationHandler(JsonObject root) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json")
            });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
