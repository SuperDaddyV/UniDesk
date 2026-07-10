using UniDesk.Helpers;

namespace UniDesk.Tests;

public class SingleInstanceHelperTests
{
    [Fact]
    public async Task SecondInstance_ShouldSignalFirstInstance()
    {
        var name = $"UniDesk.Tests.{Guid.NewGuid():N}";
        using var first = new SingleInstanceHelper(name);
        using var second = new SingleInstanceHelper(name);
        var activated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.ActivationRequested += () => activated.TrySetResult(true);

        Assert.True(first.TryAcquire());
        first.StartListening();
        Assert.False(second.TryAcquire());
        Assert.True(await second.SignalExistingInstanceAsync(CancellationToken.None));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }
}
