using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public sealed class ModelRadarPublicationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "UniDesk.PublicationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Publication_ShouldSelectBestEffortPerCategoryBeforeTakingFive_AndKeepOfficialValue()
    {
        var entries = new[]
        {
            Entry("a-max", "model-a", "max", 88.50, 97, 94, 95, 73, 61),
            Entry("a-xhigh", "model-a", "xhigh", 88.80, 95, 96, 95, 75, 61),
            Entry("a-low", "model-a", "low", 85.25, 90, 94, 85, 78, 70),
            Entry("b", "model-b", "max", 87.55, 84, 89, 100, 73, 70),
            Entry("c", "model-c", "high", 87.25, 85, 91, 95, 80, 68),
            Entry("d", "model-d", "max", 86.60, 95, 96, 90, 66, 70),
            Entry("e", "model-e", "high", 82.30, 86, 89, 85, 73, 66),
            Entry("f", "model-f", "high", 80, 80, 80, 80, 80, 80)
        };
        entries[2]["decisionTags"] = new JsonArray("value");
        using var service = Service(Publication(entries));

        var result = await service.RefreshAsync();

        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        var snapshot = Assert.IsType<ModelRadarSnapshot>(result.Snapshot);
        Assert.Equal("a-xhigh", snapshot.OverallLeader!.Id);
        Assert.Equal(88.80, snapshot.OverallLeader.OverallScore);
        Assert.Equal(new[] { "a-xhigh", "b", "c", "d", "e" }, snapshot.OverallTop.Select(item => item.Entry.Id));
        Assert.Equal("a-max", snapshot.BackendTop[0].Entry.Id);
        Assert.Equal(new[] { "a-xhigh", "d" }, snapshot.FrontendTop.Take(2).Select(item => item.Entry.Id));
        Assert.Equal("a-low", snapshot.ValueRecommendation!.Id);
        Assert.Equal("a-low", snapshot.AestheticTop.Single(item => item.Entry.Model == "model-a").Entry.Id);
        Assert.Equal(80, snapshot.AestheticTop[0].Score);
        Assert.Equal(80, snapshot.MaintainabilityTop[0].Score);
        Assert.Equal("publication-1", snapshot.BatchId);
        Assert.False(snapshot.IsLegacySnapshot);
    }

    [Fact]
    public async Task Publication_ShouldExcludeHiddenAndBotEntries_WithoutInventingValueRecommendation()
    {
        var hidden = Entry("hidden-id", "hidden-model", "max", 100, 100, 100, 100, 100, 100);
        var bot = Entry("bot-id", "bot", "unknown", 100, 100, 100, 100, 100, 100);
        bot["product_kind"] = "bot";
        var root = Publication(hidden, bot, Entry("model", "visible", "high", 80, 80, 80, 80, 80, 80));
        root["hiddenModelIds"] = new JsonArray("hidden-model");
        using var service = Service(root);
        var result = await service.RefreshAsync();

        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        Assert.Equal("model", Assert.Single(result.Snapshot!.OverallTop).Entry.Id);
        Assert.Null(result.Snapshot.ValueRecommendation);
    }

    [Fact]
    public async Task Publication_ShouldKeepMissingScoresOutOfTheirLists_AndRoundTripNewCache()
    {
        var root = Publication(
            Entry("partial", "partial-model", "high", null, 95, null, null, null, null),
            Entry("zero", "zero-model", "low", 0, 0, 0, 0, 0, 0));
        root["displayNames"]!["zero-model"] = "Zero Display";
        root["metrics"]!["zero"] = new JsonObject
        {
            ["overall"] = new JsonObject
            {
                ["elapsed_ms"] = 1234, ["estimated_api_cost_usd"] = 0.50, ["cost_coverage"] = "partial"
            }
        };
        using var service = Service(root);
        var result = await service.RefreshAsync();
        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        Assert.Equal("zero", Assert.Single(result.Snapshot!.OverallTop).Entry.Id);
        Assert.Equal("partial", result.Snapshot.BackendTop[0].Entry.Id);
        Assert.Equal("zero", Assert.Single(result.Snapshot.AestheticTop).Entry.Id);
        Assert.Equal(0, result.Snapshot.OverallLeader!.OverallScore);
        Assert.Equal("Zero Display", result.Snapshot.OverallLeader.ModelDisplayName);
        Assert.Equal("zero-model", result.Snapshot.OverallLeader.Model);
        Assert.Equal(1234, result.Snapshot.OverallLeader.ElapsedMilliseconds);
        Assert.Null(result.Snapshot.OverallLeader.EstimatedReferenceCostUsd);
        var cached = await service.ReadCacheAsync();
        Assert.Equal(ModelRadarServiceStatus.Success, cached.Status);
        Assert.Equal(result.Snapshot.AestheticTop, cached.Snapshot!.AestheticTop);
        Assert.False(cached.Snapshot.IsLegacySnapshot);
    }

    [Fact]
    public async Task Publication_WithNoOverall_ShouldKeepPublishedDimensionScores()
    {
        using var service = Service(Publication(Entry("partial", "partial-model", "high", null, 95, 70, null, 60, 80)));
        var result = await service.RefreshAsync();
        Assert.Equal(ModelRadarServiceStatus.Success, result.Status);
        Assert.True(result.Snapshot!.IsPending);
        Assert.Null(result.Snapshot.OverallLeader);
        Assert.Equal(70, Assert.Single(result.Snapshot.FrontendTop).Score);
        Assert.Equal(60, Assert.Single(result.Snapshot.AestheticTop).Score);
        Assert.Empty(result.Snapshot.KnowledgeTop);
    }

    [Theory]
    [InlineData("batch")]
    [InlineData("policy")]
    [InlineData("weight")]
    [InlineData("schema")]
    [InlineData("date")]
    [InlineData("score")]
    [InlineData("partial-overall")]
    [InlineData("duplicate")]
    public async Task Publication_ShouldRejectInvalidDataWithoutReplacingCache(string invalid)
    {
        var root = Publication(Entry("a", "model-a", "max", 80, 80, 80, 80, 80, 80));
        var cache = Path.Combine(_directory, "cache.json");
        using (var service = Service(root))
            Assert.Equal(ModelRadarServiceStatus.Success, (await service.RefreshAsync()).Status);
        var original = await File.ReadAllBytesAsync(cache);
        var quality = root["qualityScores"]!.AsObject();
        var entry = root["snapshot"]!["entries"]![0]!;
        switch (invalid)
        {
            case "batch": quality["base_publication_key"] = "different"; break;
            case "policy": quality["policy_id"] = "unknown"; break;
            case "weight": quality["weights"]!["backend"] = 40; break;
            case "schema": quality["schema_version"] = 2; break;
            case "date": root["snapshot"]!["published_at"] = "invalid"; break;
            case "score": entry["aesthetic_score"] = 101; break;
            case "partial-overall": entry["maintainability_score"] = null; break;
            case "duplicate": root["snapshot"]!["entries"]!.AsArray().Add(entry.DeepClone()); break;
        }
        using var invalidService = Service(root);
        Assert.Equal(ModelRadarServiceStatus.SchemaError, (await invalidService.RefreshAsync()).Status);
        Assert.Equal(original, await File.ReadAllBytesAsync(cache));
    }

    private ModelRadarService Service(JsonObject root) => new(
        new HttpClient(new PublicationHandler(root)), Path.Combine(_directory, "cache.json"), ownsHttpClient: true);

    internal static JsonObject Publication(params JsonObject[] entries) => new()
    {
        ["publicationKey"] = "sha256:publication-one",
        ["snapshot"] = new JsonObject
        {
            ["batch_id"] = "publication-1",
            ["published_at"] = "2026-10-15T05:43:51.078Z",
            ["entries"] = new JsonArray(entries.Cast<JsonNode>().ToArray())
        },
        ["qualityScores"] = new JsonObject
        {
            ["schema_version"] = 1,
            ["policy_id"] = "five-axis-b20-f20-k35-a15-m10-cube-half_up-v2",
            ["base_publication_key"] = "sha256:publication-one",
            ["weights"] = new JsonObject
            {
                ["backend"] = 20, ["frontend"] = 20, ["knowledge"] = 35,
                ["aesthetic"] = 15, ["maintainability"] = 10
            }
        },
        ["hiddenModelIds"] = new JsonArray(),
        ["displayNames"] = new JsonObject(),
        ["metrics"] = new JsonObject()
    };

    internal static JsonObject Entry(string id, string model, string effort, double? overall,
        double? backend, double? frontend, double? knowledge, double? aesthetic, double? maintainability) => new()
    {
        ["candidate_id"] = id, ["model_id"] = model, ["reasoning_effort"] = effort,
        ["connection_id"] = "reference", ["score_policy"] = "five-axis",
        ["overall_score"] = overall, ["backend_score"] = backend, ["frontend_score"] = frontend,
        ["knowledge_score"] = knowledge, ["aesthetic_score"] = aesthetic, ["maintainability_score"] = maintainability
    };

    private sealed class PublicationHandler(JsonObject root) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://modeldial.com/data/overview.json", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json")
            });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
