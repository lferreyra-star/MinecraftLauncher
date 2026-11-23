using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using XdLauncher.Services;

namespace XdLauncher.Views.Dialogs;

public partial class MessageDialog : Window
{
    private readonly string _context;

    public MessageDialog(string title, string message, DialogKind kind,
        string confirmText = "Entendido", string cancelText = "Cancelar")
    {
        InitializeComponent();

        _context = message;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;

        var (glyph, brushKey) = kind switch
        {
            DialogKind.Error => ("\uEA39", "ErrorBrush"),
            DialogKind.Success => ("\uE73E", "SuccessBrush"),
            DialogKind.Question => ("\uE897", "InfoBrush"),
            DialogKind.Danger => ("\uE7BA", "WarningBrush"),
            _ => ("\uE946", "InfoBrush")
        };

        IconGlyph.Text = glyph;
        if (TryFindResource(brushKey) is Brush brush)
            IconGlyph.Foreground = brush;

        if (kind is DialogKind.Info or DialogKind.Success or DialogKind.Error)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            ConfirmButton.Margin = new Thickness(0);
        }

        if (kind == DialogKind.Danger)
            ConfirmButton.Style = (Style)FindResource("DangerButton");

        if (kind is DialogKind.Error or DialogKind.Danger)
            CopyDiagnosticsButton.Visibility = Visibility.Visible;
    }

    public static bool Show(Window? owner, string title, string message, DialogKind kind = DialogKind.Info,
        string confirmText = "Entendido", string? cancelText = null)
    {
        var dialog = new MessageDialog(title, message, kind, confirmText, cancelText ?? "Cancelar");
        if (owner is { IsLoaded: true })
            dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        Core.DiagnosticService.Copy(_context);
        ToastHub.Success("Diagnóstico copiado al portapapeles.");
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
