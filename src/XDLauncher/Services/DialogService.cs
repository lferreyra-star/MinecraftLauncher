using System.Windows;
using Microsoft.Win32;
using XdLauncher.Views.Dialogs;

namespace XdLauncher.Services;

public sealed class DialogService
{
    public void ShowMessage(string title, string message, DialogKind kind = DialogKind.Info)
    {
        var dialog = new MessageDialog(title, message, kind) { Owner = GetOwner() };
        dialog.ShowDialog();
    }

    public void ShowError(string title, string message)
        => ShowMessage(title, message, DialogKind.Error);

    public bool Confirm(string title, string message, string confirmText = "Continuar",
        string cancelText = "Cancelar", bool danger = false)
    {
        var dialog = new MessageDialog(title, message, danger ? DialogKind.Danger : DialogKind.Question,
            confirmText, cancelText) { Owner = GetOwner() };
        return dialog.ShowDialog() == true;
    }

    public string? Prompt(string title, string label, string initialValue = "", string? hint = null)
    {
        var dialog = new PromptDialog(title, label, initialValue, hint) { Owner = GetOwner() };
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    public string? PickImage(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Todos los archivos|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }

    public string? PickJavaExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona java.exe",
            Filter = "Java (java.exe)|java.exe|Ejecutables|*.exe|Todos los archivos|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };
        return dialog.ShowDialog(GetOwner()) == true ? dialog.FolderName : null;
    }

    public IReadOnlyList<string>? PickAddonFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona complementos de Minecraft",
            Filter = "Complementos de Minecraft|*.mcpack;*.mcaddon;*.mcworld;*.mctemplate|Todos los archivos|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileNames : null;
    }

    public void ShowVerification(Models.VerificationReport report)
    {
        var dialog = new VerificationDialog(report) { Owner = GetOwner() };
        dialog.ShowDialog();
    }

    public void ShowImportResults(IReadOnlyList<Models.AddonImportResult> results)
    {
        var dialog = new ImportResultDialog(results) { Owner = GetOwner() };
        dialog.ShowDialog();
    }

    private static Window? GetOwner()
    {
        var main = Application.Current?.MainWindow;
        return main is { IsLoaded: true } ? main : null;
    }
}

public enum DialogKind
{
    Info,
    Question,
    Danger,
    Error,
    Success
}
