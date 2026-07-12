using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UniDesk.Controls.Settings;

public partial class AppearanceSettingsPage : UserControl
{
    public AppearanceSettingsPage()
    {
        InitializeComponent();
    }

    private void EditableTextBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.IsKeyboardFocusWithin)
        {
            return;
        }

        textBox.Focus();
        Keyboard.Focus(textBox);
        e.Handled = true;
    }
}
