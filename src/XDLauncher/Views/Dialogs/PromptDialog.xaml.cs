using System.Windows;
using System.Windows.Input;

namespace XdLauncher.Views.Dialogs;

public partial class PromptDialog : Window
{
    public PromptDialog(string title, string label, string initialValue = "", string? hint = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        LabelText.Text = label;
        ValueBox.Text = initialValue;
        HintText.Text = hint ?? string.Empty;
        HintText.Visibility = string.IsNullOrWhiteSpace(hint) ? Visibility.Collapsed : Visibility.Visible;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public string Value => ValueBox.Text.Trim();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            ErrorText.Text = "Escribe un valor para continuar.";
            ErrorText.Visibility = Visibility.Visible;
            ValueBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ValueBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirm_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
