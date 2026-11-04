using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using UniDesk.Helpers;

namespace UniDesk.Controls;

public partial class ModelRadarModuleView : UserControl
{
    public ModelRadarModuleView()
    {
        InitializeComponent();
    }

    private void OfficialLink_OnRequestNavigate(object sender, RequestNavigateEventArgs args)
    {
        args.Handled = true;
        if (!ModelRadarLinkBuilder.IsAllowedNavigationUri(args.Uri))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ModelRadarModuleView.OfficialLink_OnRequestNavigate");
        }
    }
}
