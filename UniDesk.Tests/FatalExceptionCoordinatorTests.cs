using UniDesk.Helpers;

namespace UniDesk.Tests;

public class FatalExceptionCoordinatorTests
{
    [Fact]
    public void TryBeginShutdown_ShouldSucceedOnlyOnce()
    {
        var coordinator = new FatalExceptionCoordinator();

        Assert.True(coordinator.TryBeginShutdown());
        Assert.False(coordinator.TryBeginShutdown());
    }
}
