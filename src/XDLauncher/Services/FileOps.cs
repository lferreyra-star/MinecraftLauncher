using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XdLauncher.Services;

public static class FileOps
{
    /// <summary>Sends a file or folder to the Windows Recycle Bin.</summary>
    public static bool MoveToRecycleBin(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return false;

        try
        {
            var operation = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + "\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
            };
            var result = SHFileOperation(ref operation);
            return result == 0 && !operation.fAnyOperationsAborted;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"No se pudo enviar a la papelera: {path}");
            return false;
        }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
                Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"No se pudo abrir la carpeta: {path}");
            ToastHub.Error("No se pudo abrir la carpeta.");
        }
    }

    /// <summary>Selects a file in Explorer.</summary>
    public static void ShowInExplorer(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                OpenFolder(Path.GetDirectoryName(filePath) ?? Paths.AppDataRoot);
                return;
            }
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"No se pudo mostrar el archivo: {filePath}");
        }
    }

    public static bool OpenUrl(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"No se pudo abrir la URL: {url}");
            ToastHub.Error("Windows no pudo abrir el enlace.");
            return false;
        }
    }

    public static bool OpenStoreProduct(string productId)
    {
        if (!OpenUrl($"ms-windows-store://pdp/?productid={productId}"))
            return OpenUrl(AppInfo.StoreWebUrl);
        return true;
    }

    public static bool OpenStoreUpdates()
    {
        if (!OpenUrl("ms-windows-store://downloadsandupdates"))
        {
            ToastHub.Warning("No se pudo abrir Microsoft Store. Abriendo el sitio web oficial.");
            return OpenUrl(AppInfo.StoreWebUrl);
        }
        return true;
    }

    public static bool OpenStoreSearch(string query)
        => OpenUrl($"ms-windows-store://search/?query={Uri.EscapeDataString(query)}");

    public static void CopyText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            ToastHub.Success("Copiado al portapapeles.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo copiar al portapapeles.");
            ToastHub.Error("No se pudo copiar al portapapeles.");
        }
    }

    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
