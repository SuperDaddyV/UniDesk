using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using UniDesk.Helpers;

namespace UniDesk.Controls.Settings;

public partial class GeneralSettingsPage : UserControl
{
    public GeneralSettingsPage()
    {
        InitializeComponent();
    }

    private void WeatherSourceLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "GeneralSettingsPage.OpenWeatherSource");
        }

        e.Handled = true;
    }
}
