namespace UniDesk.Services;

public interface INotificationService
{
    void ShowInfoMessage(string message);
    void ShowWarningMessage(string message);
    void ShowErrorMessage(string message);
    void ShowSuccessMessage(string message);
    bool ShowConfirmDialog(string message, string? title = null);
}
