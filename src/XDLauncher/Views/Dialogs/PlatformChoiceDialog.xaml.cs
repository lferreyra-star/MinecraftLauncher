using System.Windows;
using System.Windows.Input;

namespace XdLauncher.Views.Dialogs;

public enum PlatformChoice
{
    Cancelled,
    Windows,
    Mobile
}

/// <summary>Asks on startup whether the user wants the Windows game or the phone streaming.</summary>
public partial class PlatformChoiceDialog : Window
{
    public PlatformChoiceDialog()
    {
        InitializeComponent();
    }

    public PlatformChoice Choice { get; private set; } = PlatformChoice.Cancelled;

    public bool AskAgain { get; private set; } = true;

    /// <summary>Shows the dialog and returns it so the caller can read the choice and the checkbox.</summary>
    public static PlatformChoiceDialog Ask()
    {
        var dialog = new PlatformChoiceDialog();
        dialog.ShowDialog();
        return dialog;
    }

    private void Windows_Click(object sender, RoutedEventArgs e)
    {
        Choice = PlatformChoice.Windows;
        AskAgain = AskAgainCheck.IsChecked == true;
        DialogResult = true;
    }

    private void Mobile_Click(object sender, RoutedEventArgs e)
    {
        Choice = PlatformChoice.Mobile;
        AskAgain = AskAgainCheck.IsChecked == true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = PlatformChoice.Cancelled;
        DialogResult = false;
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
