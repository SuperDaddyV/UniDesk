using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using UniDesk.ViewModels;

namespace UniDesk.Controls.Settings;

public partial class ShortcutsSettingsPage : UserControl
{
    private SettingsViewModel? _viewModel;

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

    private void DetachViewModel()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }
}
