using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using UniDesk.ViewModels;

namespace UniDesk.Controls.Settings;

public partial class GeneralSettingsPage : UserControl
{
    private SettingsViewModel? _viewModel;

    public GeneralSettingsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => AttachViewModel(DataContext as SettingsViewModel);
        Unloaded += (_, _) => DetachViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        AttachViewModel(e.NewValue as SettingsViewModel);

    private void AttachViewModel(SettingsViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        DetachViewModel();
        _viewModel = viewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }
    }

    private void DetachViewModel()
    {
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel = null;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.IsEditingWeatherApi) ||
            _viewModel?.IsEditingWeatherApi != true)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                WeatherApiHostTextBox.Focus();
                Keyboard.Focus(WeatherApiHostTextBox);
                WeatherApiHostTextBox.SelectAll();
            });
    }

    private void WeatherApiTextBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.IsEditingWeatherApi != true ||
            sender is not TextBox textBox ||
            textBox.IsKeyboardFocusWithin)
        {
            return;
        }

        textBox.Focus();
        Keyboard.Focus(textBox);
        e.Handled = true;
    }
}
