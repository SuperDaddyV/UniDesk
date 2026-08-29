using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using UniDesk.Helpers;

namespace UniDesk.Controls;

public partial class ModelRadarModuleView : UserControl
{
    private const string LeaderboardUrl = "https://modeldial.com/radar";
    private const string LicenseUrl = "https://modeldial.com/data-license";

    public ModelRadarModuleView()
    {
        InitializeComponent();
    }

    private void LeaderboardLink_OnRequestNavigate(object sender, RequestNavigateEventArgs args)
    {
        OpenFixedUrl(LeaderboardUrl);
        args.Handled = true;
    }

    private void LicenseLink_OnRequestNavigate(object sender, RequestNavigateEventArgs args)
    {
        OpenFixedUrl(LicenseUrl);
        args.Handled = true;
    }

    private static void OpenFixedUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ModelRadarModuleView.OpenFixedUrl");
        }
    }
}
