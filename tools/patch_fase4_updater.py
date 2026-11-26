import io
import re

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Services/UpdateCheckService.cs'
s = read(p)
s = s.replace('using System.Text.Json.Nodes;\n', 'using System.Text.Json.Nodes;\nusing System.Text.RegularExpressions;\n', 1)
old = '''                        // Intentar obtener SHA-256 si el asset lo declara
                        if (asset.TryGetProperty("sha256", out var sha256Prop) && sha256Prop.ValueKind == JsonValueKind.String)
                        {
                            sha256 = sha256Prop.GetString() ?? "";
                        }
'''
new = '''                        if (asset.TryGetProperty("digest", out var digestProp) && digestProp.ValueKind == JsonValueKind.String)
                        {
                            var digest = digestProp.GetString() ?? string.Empty;
                            if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                                sha256 = digest[7..];
                        }
'''
assert old in s
s = s.replace(old, new, 1)
anchor = '            // Comparar versiones usando System.Version\n'
extra = '''            if (string.IsNullOrWhiteSpace(sha256))
            {
                var match = Regex.Match(body, @"SHA-256[:\\s]*([a-fA-F0-9]{64})", RegexOptions.IgnoreCase);
                if (match.Success)
                    sha256 = match.Groups[1].Value;
            }

'''
assert anchor in s
s = s.replace(anchor, extra + anchor, 1)
write(p, s)

p = 'src/XDLauncher/Services/UpdateService.cs'
s = read(p)
start = s.index('    public async Task<bool> UpdateAsync(UpdateInfo info, CancellationToken cancellationToken = default)')
new_method = '''    public async Task<bool> UpdateAsync(UpdateInfo info, CancellationToken cancellationToken = default)
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
        var arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /DIR=\\"" + installDirectory + "\\"";
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
'''
end = s.index('\n    }\n', start) + len('\n    }\n')
s = s[:start] + new_method + s[end:]
write(p, s)

p = 'src/XDLauncher/ViewModels/ShellViewModel.cs'
s = read(p)
old = '''    private async Task ApplyGitHubUpdateAsync(UpdateInfo info)
    {
        try
        {
'''
new = '''    private async Task ApplyGitHubUpdateAsync(UpdateInfo info)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => _ = ApplyGitHubUpdateAsync(info));
            return;
        }

        if (!_dialogs.Confirm("Actualización disponible",
                $"Hay una nueva versión {info.LatestVersion} del launcher. ¿Descargarla e instalarla ahora?",
                "Actualizar", "Ahora no"))
            return;

        try
        {
'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)
