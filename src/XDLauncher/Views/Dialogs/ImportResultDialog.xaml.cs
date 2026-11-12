using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using XdLauncher.Models;

namespace XdLauncher.Views.Dialogs;

public partial class ImportResultDialog : Window
{
    public ImportResultDialog(IReadOnlyList<AddonImportResult> results)
    {
        InitializeComponent();

        ResultsList.ItemsSource = results;
        var success = results.Count(r => r.Success);
        var failed = results.Count - success;

        SummaryText.Text = failed == 0
            ? $"{success} elemento(s) importado(s) correctamente."
            : $"{success} correcto(s) · {failed} con problemas.";
        DetailText.Text = failed == 0
            ? "Minecraft detectará el contenido la próxima vez que abras el juego. No es necesario reiniciar Windows."
            : "Revisa los mensajes de error. Los archivos compatibles se importaron de forma independiente.";
        HintText.Text = "Los archivos originales no se modifican. El contenido se copia a la carpeta oficial com.mojang.";

        SummaryGlyph.Text = failed == 0 ? "\uE73E" : success > 0 ? "\uE7BA" : "\uEA39";
        var brushKey = failed == 0 ? "SuccessBrush" : success > 0 ? "WarningBrush" : "ErrorBrush";
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
