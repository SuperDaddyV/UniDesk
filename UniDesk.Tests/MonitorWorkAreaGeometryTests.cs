using UniDesk.Helpers;

namespace UniDesk.Tests;

public class MonitorWorkAreaGeometryTests
{
    [Fact]
    public void ConvertPixelsToDip_ShouldUseTheTargetMonitorDpiAndPreserveNegativeCoordinates()
    {
        var result = MonitorWorkAreaGeometry.ConvertPixelsToDip(
            new PixelRect(-2560, 0, 2560, 1400),
            dpiX: 144,
            dpiY: 144);

        Assert.Equal(-1706.67, result.Left, 2);
        Assert.Equal(0, result.Top);
        Assert.Equal(1706.67, result.Width, 2);
        Assert.Equal(933.33, result.Height, 2);
    }

    [Fact]
    public void SelectBest_ShouldPreferTheMonitorWithTheLargestWindowIntersection()
    {
        var secondary = Area(-1706.67, 0, 1706.67, 933.33);
        var primary = Area(0, 0, 1920, 1040, isPrimary: true);

        var selected = MonitorWorkAreaGeometry.SelectBestPhysical(
            new PixelRect(-500, 100, 600, 500),
            [primary, secondary]);

        Assert.Equal(secondary, selected);
    }

    [Fact]
    public void SelectBestPhysical_ShouldNotConfuseARightHand150PercentMonitorWithThePrimaryMonitor()
    {
        var primary = Area(
            pixelLeft: 0,
            pixelTop: 0,
            pixelWidth: 1920,
            pixelHeight: 1040,
            dpi: 96,
            isPrimary: true);
        var rightAt150Percent = Area(
            pixelLeft: 1920,
            pixelTop: 0,
            pixelWidth: 2560,
            pixelHeight: 1400,
            dpi: 144);

        var selected = MonitorWorkAreaGeometry.SelectBestPhysical(
            new PixelRect(2400, 100, 600, 600),
            [primary, rightAt150Percent]);

        Assert.Equal(rightAt150Percent, selected);
        Assert.True(rightAt150Percent.WorkArea.Left < primary.WorkArea.Right);
    }

    [Fact]
    public void SelectBest_WhenSavedMonitorWasDisconnected_ShouldUseNearestExistingMonitor()
    {
        var primary = Area(0, 0, 1920, 1040, isPrimary: true);
        var right = Area(1920, 0, 1706.67, 933.33);
        var requested = new LogicalRect(-1500, 120, 400, 600);

        var selected = MonitorWorkAreaGeometry.SelectBestPhysical(
            new PixelRect(requested.Left, requested.Top, requested.Width, requested.Height),
            [primary, right]);
        var clamped = MonitorWorkAreaGeometry.Clamp(requested, selected.WorkArea);

        Assert.Equal(primary, selected);
        Assert.Equal(0, clamped.Left);
        Assert.Equal(120, clamped.Top);
    }

    [Fact]
    public void SelectBest_ShouldNotTreatTheEmptyPartOfANonRectangularDesktopAsVisible()
    {
        var primary = Area(0, 0, 1920, 1040, isPrimary: true);
        var upperRight = Area(1920, -1080, 1920, 1040);
        var requestedInVirtualScreenGap = new LogicalRect(2300, 400, 400, 400);

        var selected = MonitorWorkAreaGeometry.SelectBestPhysical(
            new PixelRect(
                requestedInVirtualScreenGap.Left,
                requestedInVirtualScreenGap.Top,
                requestedInVirtualScreenGap.Width,
                requestedInVirtualScreenGap.Height),
            [primary, upperRight]);
        var clamped = MonitorWorkAreaGeometry.Clamp(
            requestedInVirtualScreenGap,
            selected.WorkArea);

        Assert.Equal(primary, selected);
        Assert.Equal(1520, clamped.Left);
        Assert.Equal(400, clamped.Top);
    }

    [Fact]
    public void Clamp_ShouldKeepAWindowInsideANegativeCoordinateWorkArea()
    {
        var workArea = new LogicalRect(-1706.67, 0, 1706.67, 933.33);

        var result = MonitorWorkAreaGeometry.Clamp(
            new LogicalRect(-1800, -50, 720, 620),
            workArea);

        Assert.Equal(-1706.67, result.Left, 2);
        Assert.Equal(0, result.Top);
        Assert.Equal(720, result.Width);
        Assert.Equal(620, result.Height);
    }

    private static MonitorWorkArea Area(
        double left,
        double top,
        double width,
        double height,
        bool isPrimary = false) =>
        new(
            Handle: 0,
            PixelWorkArea: new PixelRect(left, top, width, height),
            WorkArea: new LogicalRect(left, top, width, height),
            DpiX: 96,
            DpiY: 96,
            IsPrimary: isPrimary);

    private static MonitorWorkArea Area(
        double pixelLeft,
        double pixelTop,
        double pixelWidth,
        double pixelHeight,
        double dpi,
        bool isPrimary = false) =>
        new(
            Handle: 0,
            PixelWorkArea: new PixelRect(pixelLeft, pixelTop, pixelWidth, pixelHeight),
            WorkArea: MonitorWorkAreaGeometry.ConvertPixelsToDip(
                new PixelRect(pixelLeft, pixelTop, pixelWidth, pixelHeight),
                dpi,
                dpi),
            DpiX: dpi,
            DpiY: dpi,
            IsPrimary: isPrimary);
}
