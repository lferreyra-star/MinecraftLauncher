using System.Diagnostics;
using System.Threading;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.Services;

public sealed class UpdateService
{
    public event Action<UpdateInfo>? UpdateChecked;
    public event Action<UpdateInfo, bool>? UpdateAvailable;

    public UpdateService() { }

    public void CheckForUpdatesCore() { }

    public async Task<bool> UpdateAsync(UpdateInfo info, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(info.DownloadUrl))
            return false;
        if (string.IsNullOrWhiteSpace(info.Sha256))
            throw new InvalidOperationException("El release no publica un SHA-256 válido. No se instalará la actualización.");

        var updateDirectory = Path.Combine(Path.GetTempPath(), "XDLauncherUpdate");
        Directory.CreateDirectory(updateDirectory);
        var setupPath = Path.Combine(updateDirectory, "MinecraftLauncher_Setup.exe");

        var progress = new Progress<DownloadProgress>(p => { });
        await DownloadService.DownloadAsync(info.DownloadUrl, setupPath, progress, cancellationToken);
        var actualSha256 = await DownloadService.ComputeSha256Async(setupPath, cancellationToken);
        if (!string.Equals(actualSha256, info.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La integridad del instalador falló (SHA-256).");

        var currentExe = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var installDirectory = Path.GetDirectoryName(currentExe) ?? AppContext.BaseDirectory;
        var arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /DIR=\"" + installDirectory + "\"";
        var startInfo = new ProcessStartInfo(setupPath, arguments)
        {
            UseShellExecute = true
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        using var installer = Process.Start(startInfo) ?? throw new InvalidOperationException("No se pudo iniciar el instalador de actualización.");
        await installer.WaitForExitAsync(timeout.Token);
        if (installer.ExitCode != 0)
            throw new InvalidOperationException($"El instalador de actualización terminó con código {installer.ExitCode}.");

        Process.Start(new ProcessStartInfo(currentExe) { UseShellExecute = true });
        return true;
    }
}
