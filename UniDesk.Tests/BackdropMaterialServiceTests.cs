using UniDesk.Helpers;

namespace UniDesk.Tests;

public class BackdropMaterialServiceTests
{
    [Theory]
    [InlineData(10, 0, 19045, false)]
    [InlineData(10, 0, 22000, false)]
    [InlineData(10, 0, 22621, true)]
    [InlineData(10, 0, 26100, true)]
    public void IsSupported_ShouldRequireWindows11Build22621(
        int major,
        int minor,
        int build,
        bool expected)
    {
        Assert.Equal(
            expected,
            BackdropMaterialService.IsSupported(new Version(major, minor, build)));
    }
}
