using UniDesk.Helpers;

namespace UniDesk.Tests;

public class PanelSizePolicyTests
{
    [Theory]
    [InlineData(1366, 768, 340, 560)]
    [InlineData(1920, 1080, 340, 760)]
    [InlineData(2560, 1440, 340, 840)]
    [InlineData(3840, 2160, 340, 840)]
    [InlineData(500, 400, 340, 384)]
    public void GetRecommendedSize_ShouldUseTheLogicalWorkArea(
        double workAreaWidth,
        double workAreaHeight,
        double expectedWidth,
        double expectedHeight)
    {
        var result = PanelSizePolicy.GetRecommendedSize(
            new LogicalRect(0, 0, workAreaWidth, workAreaHeight));

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Fact]
    public void GetRecommendedSize_ShouldRoundHeightToTheNearestTwenty()
    {
        var result = PanelSizePolicy.GetRecommendedSize(
            new LogicalRect(0, 0, 1000, 1015));

        Assert.Equal(720, result.Height);
    }

    [Fact]
    public void GetBounds_ShouldExposeDynamicMonitorLimits()
    {
        var normal = PanelSizePolicy.GetBounds(new LogicalRect(0, 0, 1366, 768));
        var small = PanelSizePolicy.GetBounds(new LogicalRect(0, 0, 500, 400));
        var fourK = PanelSizePolicy.GetBounds(new LogicalRect(0, 0, 3840, 2160));

        Assert.Equal(320, normal.MinWidth);
        Assert.Equal(520, normal.MaxWidth);
        Assert.Equal(560, normal.MinHeight);
        Assert.Equal(752, normal.MaxHeight);

        Assert.Equal(320, small.MinWidth);
        Assert.Equal(468, small.MaxWidth);
        Assert.Equal(384, small.MinHeight);
        Assert.Equal(384, small.MaxHeight);

        Assert.Equal(1040, fourK.MaxHeight);
    }

    [Fact]
    public void ClampActualSize_ShouldRestoreThePreferredSizeWhenReturningToALargerMonitor()
    {
        const double preferredWidth = 480;
        const double preferredHeight = 900;
        var largeWorkArea = new LogicalRect(0, 0, 1920, 1080);
        var smallWorkArea = new LogicalRect(0, 0, 400, 400);

        var onLargeMonitor = PanelSizePolicy.ClampActualSize(
            preferredWidth,
            preferredHeight,
            largeWorkArea);
        var onSmallMonitor = PanelSizePolicy.ClampActualSize(
            preferredWidth,
            preferredHeight,
            smallWorkArea);
        var afterReturning = PanelSizePolicy.ClampActualSize(
            preferredWidth,
            preferredHeight,
            largeWorkArea);

        Assert.Equal(new PanelSize(480, 900), onLargeMonitor);
        Assert.Equal(new PanelSize(368, 384), onSmallMonitor);
        Assert.Equal(new PanelSize(480, 900), afterReturning);
        Assert.Equal(480, preferredWidth);
        Assert.Equal(900, preferredHeight);
    }
}
