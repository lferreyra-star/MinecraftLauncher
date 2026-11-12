using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using XdLauncher.Models;

namespace XdLauncher.Views.Dialogs;

public partial class VerificationDialog : Window
{
    public VerificationDialog(VerificationReport report)
    {
        InitializeComponent();
        DataContext = report;
        ItemsList.ItemsSource = report.Items;

        SummaryText.Text = report.HasErrors
            ? "Se encontraron problemas que Windows puede reparar desde Microsoft Store."
            : report.HasWarnings
                ? "La instalación es funcional, con algunas advertencias."
                : "Todos los componentes comprobados están correctos.";
        CheckedText.Text = $"Comprobado el {report.CheckedAt.ToLocalTime():dd/MM/yyyy} a las {report.CheckedAt.ToLocalTime():HH:mm}";

        SummaryGlyph.Text = report.HasErrors ? "\uEA39" : report.HasWarnings ? "\uE7BA" : "\uE73E";
        var brushKey = report.HasErrors ? "ErrorBrush" : report.HasWarnings ? "WarningBrush" : "SuccessBrush";
        if (TryFindResource(brushKey) is Brush brush)
            SummaryGlyph.Foreground = brush;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
