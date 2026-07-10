using System.Windows;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Windows;

public partial class BackupImportPreviewWindow : Window
{
    public BackupImportPreviewWindow(
        BackupImportPreview preview,
        ILocalizationService localizationService)
    {
        InitializeComponent();
        DataContext = preview;
        SummaryText.Text = localizationService.Format(
            "Settings.ImportPreviewSummaryFormat",
            preview.SettingCount,
            preview.ShortcutCount,
            preview.TodoCount,
            preview.QuickNoteCount,
            preview.ClipboardHistoryCount,
            preview.TextSnippetCount);
    }

    private void ImportButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
