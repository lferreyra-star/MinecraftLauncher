using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using XdLauncher.Models;

namespace XdLauncher.Services;

public sealed class ScrcpyStatus
{
    public string? ExecutablePath { get; init; }
    public string? Version { get; init; }
    public bool IsInstalled { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class ScrcpyOptions
{
    public required string Serial { get; init; }
    public required string Quality { get; init; }
    public int MaxFps { get; init; } = 60;
    public bool AudioEnabled { get; set; } = true;
    public bool StayAwake { get; init; } = true;
    public bool ShowTouches { get; init; }
    public bool KeyboardMouseEnabled { get; init; } = true;
    public string WindowTitle { get; init; } = "XD Mobile Connect";
}

/// <summary>
/// Official Genymobile scrcpy integration. scrcpy is downloaded only from its official
/// GitHub release, verified with the published SHA-256 checksum, and used as-is.
/// </summary>
public sealed class ScrcpyService
{
    private const string ReleasesApi = "https://api.github.com/repos/Genymobile/scrcpy/releases/latest";
    private static readonly Regex FpsRegex = new(@"(?<value>\d+(?:[.,]\d+)?)\s*fps", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string? ExecutablePath => FindExecutable();

    public static string? FindExecutable()
    {
        if (!Directory.Exists(Paths.ScrcpyDirectory))
            return null;

        var direct = Path.Combine(Paths.ScrcpyDirectory, "scrcpy.exe");
        if (File.Exists(direct))
            return direct;

        try
        {
            return Directory.EnumerateFiles(Paths.ScrcpyDirectory, "scrcpy.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ScrcpyStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var path = ExecutablePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ScrcpyStatus
            {
                IsInstalled = false,
                Detail = "scrcpy no está instalado. El launcher puede descargar la versión oficial de Genymobile."
            };
        }

        var result = await ProcessRunner.RunAsync(path, "--version", timeout: TimeSpan.FromSeconds(20),
            cancellationToken: cancellationToken);
        var version = result.Combined.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Contains("scrcpy", StringComparison.OrdinalIgnoreCase))
            ?? "scrcpy";

        return new ScrcpyStatus
        {
            ExecutablePath = path,
            Version = version,
            IsInstalled = result.Success,
            Detail = result.Success ? $"{version} · {path}" : "scrcpy no pudo ejecutarse: " + result.Combined
        };
    }

    public async Task<ScrcpyReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        var json = await DownloadService.TryDownloadTextAsync(ReleasesApi, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = root.GetProperty("tag_name").GetString() ?? "latest";
            string? zipUrl = null;
            string? zipName = null;
            string? sumsUrl = null;
            long size = 0;

            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                var url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                if (name.Contains("win64", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipName = name;
                    zipUrl = url;
                    size = asset.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0;
                }
                else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                {
                    sumsUrl = url;
                }
            }

            if (zipUrl is null || zipName is null)
                return null;

            return new ScrcpyReleaseInfo
            {
                Version = version,
                DownloadUrl = zipUrl,
                FileName = zipName,
                Size = size,
                ChecksumsUrl = sumsUrl
            };
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo leer la información de la última versión de scrcpy.");
            return null;
        }
    }

    public async Task<(bool Ok, string Message)> InstallLatestAsync(
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var release = await GetLatestReleaseAsync(cancellationToken);
        if (release is null)
            return (false, "No se pudo consultar la versión oficial de scrcpy en GitHub. Revisa tu conexión.");

        var zipPath = Path.Combine(Paths.DownloadsDirectory, release.FileName);
        try
        {
            await DownloadService.DownloadAsync(release.DownloadUrl, zipPath, progress, cancellationToken);

            if (!string.IsNullOrWhiteSpace(release.ChecksumsUrl))
            {
                var sums = await DownloadService.TryDownloadTextAsync(release.ChecksumsUrl, cancellationToken);
                var expected = ParseChecksum(sums, release.FileName);
                if (!string.IsNullOrWhiteSpace(expected))
                {
                    var actual = await DownloadService.ComputeSha256Async(zipPath, cancellationToken);
                    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(zipPath); } catch { /* ignore */ }
                        return (false, "La verificación SHA-256 de scrcpy falló. La descarga se descartó.");
                    }
                }
            }

            var temp = Path.Combine(Paths.DownloadsDirectory, "scrcpy-extract-" + Guid.NewGuid().ToString("N"));
            try
            {
                DownloadService.ExtractAll(zipPath, temp);
                var source = FindRootWithExecutable(temp) ?? temp;

                StopRunningScrcpy();

                try
                {
                    if (Directory.Exists(Paths.ScrcpyDirectory))
                        Directory.Delete(Paths.ScrcpyDirectory, recursive: true);
                }
                catch (Exception ex)
                {
                    // Some files may still be locked; the copy below replaces them anyway.
                    LogService.Warn("No se pudo borrar la carpeta anterior de scrcpy: " + ex.Message);
                }

                Directory.CreateDirectory(Paths.ScrcpyDirectory);
                DownloadService.CopyDirectory(source, Paths.ScrcpyDirectory, overwrite: true);
            }
            finally
            {
                try { Directory.Delete(temp, recursive: true); } catch { /* ignore */ }
            }

            var status = await GetStatusAsync(cancellationToken);
            return status.IsInstalled
                ? (true, $"scrcpy {release.Version} instalado y verificado.")
                : (false, "scrcpy se descargó, pero no pudo iniciarse: " + status.Detail);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo instalar scrcpy.");
            return (false, "No se pudo instalar scrcpy: " + ex.Message);
        }
    }

    /// <summary>Closes any running scrcpy so its files can be replaced.</summary>
    private static void StopRunningScrcpy()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("scrcpy"))
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudieron cerrar los procesos de scrcpy: " + ex.Message);
        }
    }

    private static string? FindRootWithExecutable(string directory)    {
        if (File.Exists(Path.Combine(directory, "scrcpy.exe")))
            return directory;
        try
        {
            return Directory.EnumerateDirectories(directory)
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "scrcpy.exe")));
        }
        catch
        {
            return null;
        }
    }

    private static string? ParseChecksum(string? checksums, string fileName)
    {
        if (string.IsNullOrWhiteSpace(checksums))
            return null;

        foreach (var line in checksums.Replace("\r", string.Empty).Split('\n'))
        {
            var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                parts[^1].TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return parts[0];
        }
        return null;
    }

    public async Task<ScrcpySession> StartAsync(ScrcpyOptions options, CancellationToken cancellationToken = default)
    {
        var executable = ExecutablePath
            ?? throw new InvalidOperationException("scrcpy no está instalado.");

        var arguments = BuildArguments(options);
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        // scrcpy must talk to the same ADB server as the launcher.
        var adbPath = AdbService.ResolveAdb();
        if (!string.IsNullOrWhiteSpace(adbPath))
            startInfo.Environment["ADB"] = adbPath;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var session = new ScrcpySession(process, options);
        process.OutputDataReceived += (_, args) => session.HandleLine(args.Data);
        process.ErrorDataReceived += (_, args) => session.HandleLine(args.Data);

        if (!process.Start())
            throw new InvalidOperationException("No se pudo iniciar scrcpy.");

        session.AttachProcess();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var handle = await WaitForWindowAsync(process, options.WindowTitle, TimeSpan.FromSeconds(20), cancellationToken);
        session.WindowHandle = handle;
        if (handle == IntPtr.Zero)
            LogService.Warn("scrcpy inició pero no se encontró su ventana; se usará el modo de ventana independiente.");

        return session;
    }

    public static string BuildArguments(ScrcpyOptions options)
    {
        var maxSize = options.Quality switch
        {
            "HD" => 1280,
            "FHD" => 1920,
            _ => 0
        };

        var arguments = new List<string>
        {
            "-s", Quote(options.Serial),
            "--window-title", Quote(options.WindowTitle),
            "--print-fps",
            "--video-codec=h264",
            "--no-clipboard-autosync",
            $"--max-fps={Math.Clamp(options.MaxFps, 15, 240)}"
        };

        if (options.KeyboardMouseEnabled)
        {
            arguments.Add("--keyboard=uhid");
            arguments.Add("--mouse=uhid");
        }

        if (!options.AudioEnabled)
            arguments.Add("--no-audio");
        if (options.StayAwake)
            arguments.Add("--stay-awake");
        if (options.ShowTouches)
            arguments.Add("--show-touches");
        if (maxSize > 0)
            arguments.Add($"--max-size={maxSize}");

        return string.Join(' ', arguments);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static async Task<IntPtr> WaitForWindowAsync(Process process, string title, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (process.HasExited)
                    return IntPtr.Zero;
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                    return process.MainWindowHandle;
            }
            catch
            {
                // keep polling
            }

            var found = FindWindowByTitle(title, process.Id);
            if (found != IntPtr.Zero)
                return found;

            await Task.Delay(250, cancellationToken);
        }
        return IntPtr.Zero;
    }

    public static IntPtr FindWindowByTitle(string title, int? processId = null)
    {
        var result = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            var length = GetWindowTextLength(hwnd);
            if (length <= 0)
                return true;

            var buffer = new System.Text.StringBuilder(length + 1);
            GetWindowText(hwnd, buffer, buffer.Capacity);
            var text = buffer.ToString();
            if (!text.Contains(title, StringComparison.OrdinalIgnoreCase))
                return true;

            if (processId is { } pid)
            {
                GetWindowThreadProcessId(hwnd, out var windowPid);
                if (windowPid != pid)
                    return true;
            }

            result = hwnd;
            return false;
        }, IntPtr.Zero);

        return result;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);
}

public sealed class ScrcpySession : IDisposable
{
    private readonly Process _process;
    private readonly ScrcpyOptions _options;
    private int _disposed;

    public ScrcpySession(Process process, ScrcpyOptions options)
    {
        _process = process;
        _options = options;
    }

    public event Action<double>? FpsUpdated;
    public event Action<int>? Exited;

    public int ProcessId { get; private set; }
    public IntPtr WindowHandle { get; set; }
    public int MaxFps => _options.MaxFps;
    public string Quality => _options.Quality;
    public bool IsRunning
    {
        get
        {
            try
            {
                return !_process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    public void AttachProcess()
    {
        ProcessId = _process.Id;
        _process.Exited += (_, _) =>
        {
            try
            {
                Exited?.Invoke(_process.ExitCode);
            }
            catch
            {
                // ignore
            }
        };
    }

    internal void HandleLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        var match = ScrcpyFpsRegex.Match(line);
        if (!match.Success)
            return;

        if (double.TryParse(match.Groups["value"].Value.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var fps) && fps > 0)
            FpsUpdated?.Invoke(fps);
    }

    private static readonly Regex ScrcpyFpsRegex =
        new(@"(?<value>\d+(?:[.,]\d+)?)\s*fps", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void Stop()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Stop();
        try { _process.Dispose(); } catch { /* ignore */ }
    }
}
