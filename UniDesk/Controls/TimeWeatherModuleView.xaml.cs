using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UniDesk.Models;
using UniDesk.ViewModels;

namespace UniDesk.Controls;

public partial class TimeWeatherModuleView : UserControl
{
    private TimeWeatherViewModel? ViewModel => DataContext as TimeWeatherViewModel;

    public TimeWeatherModuleView()
    {
        InitializeComponent();
    }

    private void ClockHotspot_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.ToggleCalendarPopupCommand.Execute(null);
        e.Handled = true;
    }

    private void PreviousCalendarMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.PreviousCalendarMonthCommand.Execute(null);
        e.Handled = true;
    }

    private void NextCalendarMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.NextCalendarMonthCommand.Execute(null);
        e.Handled = true;
    }

    private void CloseCalendarButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.IsCalendarPopupOpen = false;
        e.Handled = true;
    }

    private void CalendarDayButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.SelectCalendarDateCommand.Execute((sender as FrameworkElement)?.DataContext as CalendarDayItem);
        e.Handled = true;
    }
}
