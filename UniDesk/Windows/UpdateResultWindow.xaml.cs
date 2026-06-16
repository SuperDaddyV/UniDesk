using System.Windows;
using System.Windows.Input;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Windows;

public partial class UpdateResultWindow : Window
{
    private readonly ILocalizationService _localizationService;

    public UpdateResultWindow(UpdateCheckResult result, ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        InitializeComponent();
        CurrentVersionText.Text = result.CurrentVersion;
        LatestVersionText.Text = result.LatestVersion;
        PublishedAtText.Text = result.PublishedAt.HasValue
            ? localizationService.Format(
                "Update.PublishedAtFormat",
                result.PublishedAt.Value.LocalDateTime.ToString("d", localizationService.CurrentCulture))
            : string.Empty;
        ReleaseNotesText.Text = BuildReleaseNotesText(result.ReleaseNotes);
        DesktopWidgetWindowHelper.Configure(this);
    }

    public static bool Show(UpdateCheckResult result, ILocalizationService localizationService)
    {
        var window = new UpdateResultWindow(result, localizationService);
        var anchor = ToastPlacementHelper.GetAnchorWindow();
        if (anchor != null)
        {
            window.Owner = anchor;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ContentRendered += (_, _) => ToastPlacementHelper.PositionConfirmNearAnchor(window);
        return window.ShowDialog() == true;
    }

    private string BuildReleaseNotesText(string? releaseNotes)
    {
        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            return _localizationService.GetString("Update.NoReleaseNotes");
        }

        var normalized = releaseNotes.Trim();
        const int maxLength = 900;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + Environment.NewLine + "...";
    }

    private void OpenButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void LaterButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
