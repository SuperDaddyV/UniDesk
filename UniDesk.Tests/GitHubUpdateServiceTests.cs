using UniDesk.Services;

namespace UniDesk.Tests;

public class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("1.3.6", "v1.3.7", -1)]
    [InlineData("v1.10.0", "1.9.9", 1)]
    [InlineData("1.3.7+build.5", "v1.3.7", 0)]
    [InlineData("1.3.8-preview.1", "1.3.8", 0)]
    public void CompareVersionTags_ShouldCompareNormalizedSemanticVersions(
        string currentVersion,
        string latestVersion,
        int expectedSign)
    {
        var comparison = GitHubUpdateService.CompareVersionTags(currentVersion, latestVersion);

        Assert.Equal(expectedSign, Math.Sign(comparison));
    }
}
