using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using UniDesk.Helpers;
using UniDesk.Services;
using UniDesk.ViewModels;

namespace UniDesk;

public partial class QuickNoteEditorWindow : Window
{
    private readonly QuickNoteEditorViewModel _viewModel;
    private bool _closeRequested;
    private bool _allowClose;

    public QuickNoteEditorWindow(QuickNoteEditorViewModel viewModel, double panelWidth)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        var width = panelWidth - 28;
        if (width < IWindowService.MinPanelWidth - 28)
        {
            width = IWindowService.MinPanelWidth - 28;
        }

        Width = width;
        MinWidth = width;
        Loaded += (_, _) => ContentBox.Focus();
    }

    private void CopyButton_OnClick(object sender, RoutedEventArgs e) =>
        _viewModel.CopyContent();

    private async void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.DeleteAsync())
        {
            Close();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void DialogRoot_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ShouldIgnoreDrag(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private static bool ShouldIgnoreDrag(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is TextBoxBase or Button or ScrollBar or Thumb or CheckBox)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void QuickNoteEditorWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Close();
        e.Handled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closeRequested)
        {
            return;
        }

        _closeRequested = true;
        _ = FlushAndCloseAsync();
    }

    private async Task FlushAndCloseAsync()
    {
        try
        {
            if (!await _viewModel.FlushAndCleanupAsync())
            {
                _closeRequested = false;
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickNoteEditorWindow.FlushAndClose");
            _closeRequested = false;
            return;
        }

        _allowClose = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
