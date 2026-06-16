using UniDesk.Helpers;
using UniDesk.Services;
using UniDesk.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace UniDesk;

public partial class TodoEditWindow : Window
{
    private readonly TodoEditViewModel _viewModel;
    private DateTime _displayedMonth;

    public TodoEditWindow(TodoEditViewModel viewModel, double panelWidth)
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

        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        Loaded += (_, _) => UpdateToggleStyles();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TodoEditViewModel.SelectedDatePreset)
            or nameof(TodoEditViewModel.SelectedPriority))
        {
            UpdateToggleStyles();
        }

        if (e.PropertyName == nameof(TodoEditViewModel.IsDatePopupOpen) && _viewModel.IsDatePopupOpen)
        {
            _displayedMonth = new DateTime(_viewModel.DueDateTime.Year, _viewModel.DueDateTime.Month, 1);
            RefreshCalendarDays();
        }

        if (e.PropertyName is nameof(TodoEditViewModel.DueDateTime) && _viewModel.IsDatePopupOpen)
        {
            RefreshCalendarDays();
        }
    }

    private void UpdateToggleStyles()
    {
        foreach (var child in PresetPanel.Children)
        {
            if (child is not Button button) continue;

            var isActive = button.Tag?.ToString() == _viewModel.SelectedDatePreset.ToString();
            button.Style = (Style)FindResource(isActive ? "DlgPresetButtonActive" : "DlgPresetButton");
        }

        foreach (var child in PriorityPanel.Children)
        {
            if (child is not Button button) continue;

            var isActive = button.Tag?.ToString() == _viewModel.SelectedPriority.ToString();
            button.Style = (Style)FindResource(isActive ? "DlgPriorityButtonActive" : "DlgPriorityButton");
        }
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var ok = await _viewModel.SaveAsync();
        if (!ok) return;

        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DialogRoot_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ShouldIgnoreDrag(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.OriginalSource == sender)
        {
            _viewModel.ClosePopupsCommand.Execute(null);
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
            if (current is TextBoxBase or Button or ScrollBar or Thumb)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void RefreshCalendarDays()
    {
        CalendarMonthText.Text = _viewModel.FormatCalendarMonth(_displayedMonth);
        CalendarDaysHost.ItemsSource = CalendarDayBuilder.BuildMonth(_displayedMonth, _viewModel.DueDateTime.Date);
    }

    private void PrevMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(-1);
        RefreshCalendarDays();
    }

    private void NextMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(1);
        RefreshCalendarDays();
    }

    private void CalendarDay_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateTime date })
        {
            return;
        }

        _viewModel.PickCalendarDateCommand.Execute(date);
        _displayedMonth = new DateTime(date.Year, date.Month, 1);
        RefreshCalendarDays();
    }

    private void TimeChip_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string time })
        {
            return;
        }

        _viewModel.PickTimeCommand.Execute(time);
    }

    private void TimeScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void TodoEditWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (_viewModel.IsDatePopupOpen || _viewModel.IsTimePopupOpen)
        {
            _viewModel.ClosePopupsCommand.Execute(null);
            e.Handled = true;
            return;
        }

        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        base.OnClosed(e);
    }
}
