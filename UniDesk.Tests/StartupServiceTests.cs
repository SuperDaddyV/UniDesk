using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

public class StartupServiceTests
{
    [Fact]
    public void SyncWithSetting_WhenDisabled_DoesNotThrow()
    {
        var service = new StartupService(new NoOpNotificationService());
        var exception = Record.Exception(() => service.SyncWithSetting(false));
        Assert.Null(exception);
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => false;
    }
}
