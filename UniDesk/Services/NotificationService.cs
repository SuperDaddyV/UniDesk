using System.Windows;
using System.Windows.Threading;
using UniDesk.Helpers;
using UniDesk.Windows;

namespace UniDesk.Services;

public class NotificationService : INotificationService
{
    private readonly object _toastLock = new();
    private readonly List<ToastWindow> _activeToasts = new();
    private readonly ILocalizationService _localizationService;

    public NotificationService(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public void ShowInfoMessage(string message) => ShowToast(message, ToastKind.Info, 3500);

    public void ShowWarningMessage(string message) => ShowToast(message, ToastKind.Warning, 5000);

    public void ShowErrorMessage(string message) => ShowToast(message, ToastKind.Error, 6000);

    public void ShowSuccessMessage(string message) => ShowToast(message, ToastKind.Success, 3500);

    public bool ShowConfirmDialog(string message, string? title = null) =>
        CompactConfirmWindow.Show(message, string.IsNullOrWhiteSpace(title)
            ? _localizationService.GetString("Common.Confirm")
            : title);

    private void ShowToast(string message, ToastKind kind, int durationMs)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(() => EnqueueToast(message, kind, durationMs));
    }

    private void EnqueueToast(string message, ToastKind kind, int durationMs)
    {
        lock (_toastLock)
        {
            var stackIndex = _activeToasts.Count;
            var toast = new ToastWindow(message, kind);
            toast.Closed += (_, _) =>
            {
                lock (_toastLock)
                {
                    _activeToasts.Remove(toast);
                    RepositionToasts();
                }
            };

            _activeToasts.Add(toast);
            toast.ShowWithAutoClose(durationMs, stackIndex);
        }
    }

    private void RepositionToasts()
    {
        for (var i = 0; i < _activeToasts.Count; i++)
        {
            ToastPlacementHelper.PositionNearAnchor(_activeToasts[i], stackIndex: i);
        }
    }
}
