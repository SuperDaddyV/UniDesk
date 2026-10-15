using UniDesk.Helpers;

namespace UniDesk.Tests;

public sealed class CompactPanelWidthTests
{
    [Theory]
    [InlineData(320)]
    [InlineData(340)]
    [InlineData(520)]
    public void RequestedWidth_ShouldPreserveTheSavedWidth_WithoutAccumulation(double preferred)
    {
        for (var index = 0; index < 5; index++)
        {
            Assert.Equal(preferred, PanelSizePolicy.GetRequestedWidth(preferred));
        }
        Assert.Equal(preferred, PanelSizePolicy.ClampPreferredWidth(preferred));
    }

    [Theory]
    [InlineData(1920, 320, 520)]
    [InlineData(400, 320, 368)]
    [InlineData(350, 318, 318)]
    public void Bounds_ShouldUseOriginalWidthLimits_AndRespectWorkArea(double workWidth, double min, double max)
    {
        var bounds = PanelSizePolicy.GetBounds(new LogicalRect(0, 0, workWidth, 1080));
        Assert.Equal(min, bounds.MinWidth);
        Assert.Equal(max, bounds.MaxWidth);
    }
}
