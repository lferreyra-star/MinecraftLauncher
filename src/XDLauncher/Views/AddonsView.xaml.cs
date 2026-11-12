using System.Windows;
using System.Windows.Controls;
using XdLauncher.Services;
using XdLauncher.ViewModels;

namespace XdLauncher.Views;

public partial class AddonsView : UserControl
{
    public AddonsView()
    {
        InitializeComponent();
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not AddonsViewModel viewModel)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        var supported = new[] { ".mcpack", ".mcaddon", ".mcworld", ".mctemplate" };
        var accepted = files
            .Where(File.Exists)
            .Where(f => supported.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (accepted.Count == 0)
        {
            ToastHub.Warning("Arrastra archivos .mcpack, .mcaddon, .mcworld o .mctemplate.");
            return;
        }

        await viewModel.ImportFilesAsync(accepted);
    }
}
