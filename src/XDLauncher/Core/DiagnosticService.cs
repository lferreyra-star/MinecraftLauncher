using System.Text.RegularExpressions;
using XdLauncher.Services;

namespace XdLauncher.Core;

public static class DiagnosticService
{
    public static string BuildReport(string? context = null)
    {
        var info = new SystemInfoService().GetSummary();
        var launcherMemory = new SystemInfoService().GetLauncherMemoryBytes();
        var text =
            $"{AppInfo.ProductName} {AppInfo.FullVersion}{Environment.NewLine}" +
            $"Windows: {info.WindowsName} ({info.OsArchitecture}){Environment.NewLine}" +
            $"CPU: {info.CpuName}{Environment.NewLine}" +
            $"GPU: {info.GpuName}{Environment.NewLine}" +
            $"RAM: {info.TotalRamText} totales · {info.AvailableRamText} disponibles · {info.RamUsageText}{Environment.NewLine}" +
            $"Launcher en memoria: {launcherMemory / 1024d / 1024d:0.#} MB{Environment.NewLine}" +
            $"Registro: {LogService.CurrentFile ?? Paths.LogsDirectory}{Environment.NewLine}" +
            (!string.IsNullOrWhiteSpace(context) ? $"Contexto: {context}{Environment.NewLine}" : string.Empty);
        return Scrub(text);
    }

    public static void Copy(string? context = null) => FileOps.CopyText(BuildReport(context));

    private static string Scrub(string text)
    {
        text = Regex.Replace(text, @"[\w\.\-]+@[\w\.\-]+\.\w+", "<email>");
        text = Regex.Replace(text, @"C:\\Users\\[^\\\s]+", @"C:\Users\<user>");
        return text;
    }
}
