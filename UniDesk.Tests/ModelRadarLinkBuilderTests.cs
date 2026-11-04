using System.Reflection;
using UniDesk.Models;

namespace UniDesk.Tests;

public sealed class ModelRadarLinkBuilderTests
{
    [Theory]
    [InlineData(ModelRadarCategory.Overall, "overall")]
    [InlineData(ModelRadarCategory.Backend, "backend")]
    [InlineData(ModelRadarCategory.Frontend, "frontend")]
    [InlineData(ModelRadarCategory.Knowledge, "knowledge")]
    [InlineData(ModelRadarCategory.Aesthetic, "aesthetic")]
    [InlineData(ModelRadarCategory.Maintainability, "maintainability")]
    public void FullRanking_ShouldPreserveTheSelectedCategory(ModelRadarCategory category, string axis)
    {
        var uri = Assert.IsType<Uri>(Invoke("BuildFullRankingUri", category));

        Assert.Equal($"https://modeldial.com/radar?axis={axis}", uri.AbsoluteUri);
        Assert.True(IsAllowed(uri));
    }

    [Fact]
    public void ModelDetail_ShouldUseRawModelAndEffortAndEncodeOnePathSegment()
    {
        var uri = Assert.IsType<Uri>(Invoke("BuildModelDetailUri", "gpt-6.1-sol", "default", ModelRadarCategory.Backend));
        Assert.Equal("https://modeldial.com/models/gpt-6.1-sol?axis=backend&config=gpt-6.1-sol~default", uri.AbsoluteUri);
        Assert.True(IsAllowed(uri));

        var unicode = Assert.IsType<Uri>(Invoke("BuildModelDetailUri", "模型-alpha", "high", ModelRadarCategory.Aesthetic));
        Assert.Equal("/models/" + Uri.EscapeDataString("模型-alpha"), unicode.AbsolutePath);
        Assert.True(IsAllowed(unicode));

        var longModel = new string('m', 240) + ".v2";
        var longUri = Assert.IsType<Uri>(Invoke("BuildModelDetailUri", longModel, "xhigh", ModelRadarCategory.Overall));
        Assert.Equal("/models/" + longModel, longUri.AbsolutePath);
        Assert.True(IsAllowed(longUri));
    }

    [Theory]
    [InlineData("anthropic/claude-sonnet-5.5")]
    [InlineData("meta/muse-wonder")]
    [InlineData("anthropic/haiku-5.5")]
    [InlineData("组织_1/模型-v2.3")]
    [InlineData("provider/family/model")]
    [InlineData("nvidia/nemotron-3-ultra-550b-a55b:free")]
    public void ModelDetail_ShouldPreserveProviderNamespacesInsideOneEncodedPathSegment(string model)
    {
        var uri = Assert.IsType<Uri>(Invoke("BuildModelDetailUri", model, "high", ModelRadarCategory.Knowledge));

        Assert.Equal($"https://modeldial.com/models/{Uri.EscapeDataString(model)}?axis=knowledge&config={Uri.EscapeDataString(model)}~high", uri.AbsoluteUri);
        Assert.Equal(3, uri.Segments.Length);
        Assert.True(IsAllowed(uri));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../radar")]
    [InlineData("/model")]
    [InlineData("model/")]
    [InlineData("provider//model")]
    [InlineData("provider/./model")]
    [InlineData("provider/../model")]
    [InlineData("provider/%2e%2e/model")]
    [InlineData("provider%2Fmodel")]
    [InlineData(":model")]
    [InlineData("provider/model:")]
    [InlineData("provider/model::free")]
    [InlineData("provider/model:.")]
    [InlineData("provider/model:..")]
    [InlineData("model\\other")]
    [InlineData("model?axis=backend")]
    [InlineData("model#fragment")]
    [InlineData("%2e%2e")]
    [InlineData("model~max")]
    [InlineData(" model")]
    public void ModelDetail_ShouldDisableUnsafeOrAmbiguousModelIdentifiers(string model)
    {
        Assert.Null(Invoke("BuildModelDetailUri", model, "high", ModelRadarCategory.Overall));
    }

    [Theory]
    [InlineData("")]
    [InlineData("high&redirect=https://example.com")]
    [InlineData("high/../max")]
    [InlineData("high/low")]
    [InlineData("high:free")]
    [InlineData(" high ")]
    public void ModelDetail_ShouldRejectAnEffortThatCouldChangeTheQuery(string effort)
    {
        Assert.Null(Invoke("BuildModelDetailUri", "gpt-6-astra", effort, ModelRadarCategory.Overall));
    }

    [Theory]
    [InlineData("https://modeldial.com/radar")]
    [InlineData("https://modeldial.com/data-license")]
    [InlineData("https://modeldial.com/models/gpt-6-astra?axis=backend&config=gpt-6-astra~xhigh")]
    [InlineData("https://modeldial.com/models/anthropic%2Fclaude-sonnet-5.5?axis=overall&config=anthropic%2Fclaude-sonnet-5.5~high")]
    [InlineData("https://modeldial.com/models/meta%2Fmuse-wonder?axis=knowledge&config=meta%2Fmuse-wonder~high")]
    public void Navigation_ShouldAllowOnlyKnownOfficialRoutes(string value)
    {
        Assert.True(IsAllowed(new Uri(value)));
    }

    [Theory]
    [InlineData("http://modeldial.com/radar?axis=overall")]
    [InlineData("https://example.com/radar?axis=overall")]
    [InlineData("https://modeldial.com.example.com/radar?axis=overall")]
    [InlineData("https://modeldial.com:444/radar?axis=overall")]
    [InlineData("https://user@modeldial.com/radar?axis=overall")]
    [InlineData("https://modeldial.com/radar?axis=overall#fragment")]
    [InlineData("https://modeldial.com/models/../radar?axis=overall")]
    [InlineData("https://modeldial.com/models/%2e%2e/radar?axis=overall")]
    [InlineData("https://modeldial.com/models/provider/model?axis=overall&config=provider%2Fmodel~high")]
    [InlineData("https://modeldial.com/models/provider%2F..%2Fmodel?axis=overall&config=provider%2F..%2Fmodel~high")]
    [InlineData("https://modeldial.com/models/provider%2F%2E%2E%2Fmodel?axis=overall&config=provider%2F%2E%2E%2Fmodel~high")]
    [InlineData("https://modeldial.com/models/provider%252Fmodel?axis=overall&config=provider%252Fmodel~high")]
    [InlineData("https://modeldial.com/models/provider%2Fmodel?axis=overall&config=other%2Fmodel~high")]
    [InlineData("https://modeldial.com/models/provider%2Fmodel%3A..?axis=overall&config=provider%2Fmodel%3A..~high")]
    [InlineData("https://modeldial.com/models/foo?axis=overall&config=bar~high")]
    [InlineData("https://modeldial.com/models/foo?axis=overall&config=foo~high%26redirect%3Dhttps%3A%2F%2Fexample.com")]
    [InlineData("https://modeldial.com/radar?axis=overall&axis=backend")]
    [InlineData("https://modeldial.com/radar?axis=unknown")]
    [InlineData("https://modeldial.com/radar?axis=overall&redirect=https://example.com")]
    [InlineData("https://modeldial.com/data-license?redirect=https://example.com")]
    [InlineData("https://modeldial.com/compare")]
    [InlineData("/radar?axis=overall")]
    [InlineData("javascript:alert(1)")]
    public void Navigation_ShouldRejectEscapesExtraParametersAndUnapprovedDestinations(string value)
    {
        Assert.False(IsAllowed(new Uri(value, UriKind.RelativeOrAbsolute)));
    }

    [Fact]
    public void Navigation_ShouldRejectMissingUri()
    {
        Assert.False(IsAllowed(null));
    }

    private static bool IsAllowed(Uri? uri) => Assert.IsType<bool>(Invoke("IsAllowedNavigationUri", uri));

    private static object? Invoke(string method, params object?[] arguments)
    {
        var type = typeof(ModelRadarCategory).Assembly.GetType("UniDesk.Helpers.ModelRadarLinkBuilder");
        Assert.NotNull(type);
        var entry = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(entry);
        return entry.Invoke(null, arguments);
    }
}
