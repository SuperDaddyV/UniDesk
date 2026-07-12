using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UniDesk.Helpers;
using UniDesk.ViewModels;

namespace UniDesk.Controls.Settings;

public partial class ShortcutsSettingsPage : UserControl
{
    private SettingsViewModel? _viewModel;
    private bool _isRecordingHotkey;

    public ShortcutsSettingsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => UpdateChipStyles();
        Unloaded += (_, _) => DetachViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();
        _viewModel = e.NewValue as SettingsViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        UpdateChipStyles();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ShortcutMaxCount))
        {
            UpdateChipStyles();
        }
    }

    private void UpdateChipStyles()
    {
        if (_viewModel == null)
        {
            return;
        }

        foreach (var child in ShortcutLimitPanel.Children.OfType<Button>())
        {
            var active = child.Tag?.ToString() == _viewModel.ShortcutMaxCount.ToString();
            child.Style = (Style)FindResource(active ? "GlassChipButtonActiveStyle" : "GlassChipButtonStyle");
        }
    }

    private void RecordHotkey_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.GlobalHotkeyEnabled != true)
        {
            return;
        }

        _isRecordingHotkey = true;
        _viewModel.HotkeyStatusText = FindResource("Settings.HotkeyRecording") as string ?? string.Empty;
        HotkeyCaptureBox.Focus();
        Keyboard.Focus(HotkeyCaptureBox);
    }

    private void HotkeyCaptureBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecordingHotkey || _viewModel == null)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            StopHotkeyRecording(string.Empty);
            return;
        }

        if (key is Key.Back or Key.Delete)
        {
            _viewModel.Hotkey = string.Empty;
            StopHotkeyRecording(FindResource("Hotkey.InvalidCapture") as string ?? string.Empty);
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        if (HotkeyGestureParser.TryCreate(key, Keyboard.Modifiers, out var gesture))
        {
            _viewModel.Hotkey = gesture.DisplayText;
            StopHotkeyRecording(string.Empty);
            return;
        }

        _viewModel.HotkeyStatusText = FindResource("Hotkey.InvalidCapture") as string ?? string.Empty;
    }

    private void StopHotkeyRecording(string statusText)
    {
        _isRecordingHotkey = false;
        if (_viewModel != null)
        {
            _viewModel.HotkeyStatusText = statusText;
        }
    }

    private void DetachViewModel()
    {
        _isRecordingHotkey = false;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }
}
