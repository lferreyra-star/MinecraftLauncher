using System.Diagnostics;
using System.Text.RegularExpressions;
using XdLauncher.Models;

namespace XdLauncher.Services;

public sealed class AdbStatus
{
    public string? AdbPath { get; init; }
    public string? Version { get; init; }
    public bool IsAvailable { get; init; }
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// Official Android Debug Bridge integration. All device authorisation and pairing is handled
/// by Android itself; the launcher never bypasses the ADB trust prompt or installs device apps.
/// </summary>
public sealed class AdbService
{
    private static readonly Regex SizeRegex = new(@"size:\s*(\d+x\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Ipv4Regex = new(@"\b(\d{1,3}(?:\.\d{1,3}){3})\b", RegexOptions.Compiled);
    private string? _adbPath;

    public string? AdbPath => _adbPath ??= ResolveAdb();

    public static string? ResolveAdb()
    {
        // The copy installed by the launcher has priority: it is the version the app
        // downloads and keeps up to date. The system SDK is used as a fallback.
        var bundled = Path.Combine(Paths.PlatformToolsDirectory, "adb.exe");
        if (File.Exists(bundled))
            return bundled;

        foreach (var root in Paths.CandidateSdkRoots())
        {
            var candidate = Path.Combine(root, "platform-tools", "adb.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        foreach (var pathDirectory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                 .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(pathDirectory.Trim(), "adb.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // ignore malformed PATH entries
            }
        }

        return null;
    }

    public async Task<AdbStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var path = AdbPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return new AdbStatus
            {
                IsAvailable = false,
                Detail = "ADB no está instalado. Descárgalo con el botón INSTALAR ADB (platform-tools oficial de Google)."
            };
        }

        var result = await ProcessRunner.RunAsync(path, "version", timeout: TimeSpan.FromSeconds(20),
            cancellationToken: cancellationToken);
        if (!result.Success)
        {
            return new AdbStatus
            {
                AdbPath = path,
                IsAvailable = false,
                Detail = "ADB existe pero no pudo ejecutarse: " + (result.Combined.Length > 0 ? result.Combined : "error desconocido")
            };
        }

        var version = result.StdOut
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("Version", StringComparison.OrdinalIgnoreCase))
            ?? result.StdOut.Split('\n').FirstOrDefault()?.Trim()
            ?? "ADB";

        return new AdbStatus
        {
            AdbPath = path,
            Version = version,
            IsAvailable = true,
            Detail = $"{version} · {path}"
        };
    }

    public async Task<bool> StartServerAsync(CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return false;
        var result = await ProcessRunner.RunAsync(AdbPath, "start-server", timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);
        return result.Success;
    }

    public async Task<List<AndroidDeviceInfo>> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = new List<AndroidDeviceInfo>();
        if (AdbPath is null)
            return devices;

        await StartServerAsync(cancellationToken);
        var result = await ProcessRunner.RunAsync(AdbPath, "devices -l", timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);
        if (!result.Success)
            return devices;

        foreach (var rawLine in result.StdOut.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("*", StringComparison.Ordinal))
                continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            var serial = parts[0];
            var state = parts[1];
            var model = parts.Skip(2)
                .FirstOrDefault(p => p.StartsWith("model:", StringComparison.OrdinalIgnoreCase))?[6..];
            var product = parts.Skip(2)
                .FirstOrDefault(p => p.StartsWith("product:", StringComparison.OrdinalIgnoreCase))?[8..];

            devices.Add(new AndroidDeviceInfo
            {
                Serial = serial,
                State = state,
                Model = model?.Replace('_', ' '),
                Product = product
            });
        }

        return devices;
    }

    public async Task<AndroidDeviceInfo> RefreshDetailsAsync(AndroidDeviceInfo device,
        CancellationToken cancellationToken = default)
    {
        if (AdbPath is null || !device.IsAuthorized)
            return device;

        var command = "getprop ro.product.manufacturer; getprop ro.product.model; " +
                      "getprop ro.build.version.release; getprop ro.build.version.sdk; wm size; ip route";
        var result = await ProcessRunner.RunAsync(AdbPath, $"-s \"{device.Serial}\" shell \"{command}\"",
            timeout: TimeSpan.FromSeconds(20), cancellationToken: cancellationToken);
        if (!result.Success)
            return device;

        var lines = result.StdOut.Replace("\r", string.Empty).Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var index = 0;
        string? Next() => index < lines.Count ? lines[index++] : null;

        var manufacturer = Next();
        var model = Next();
        var release = Next();
        var sdkText = Next();
        var rest = lines.Skip(index).ToList();

        device.Manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? device.Manufacturer : manufacturer;
        device.Model = string.IsNullOrWhiteSpace(model) ? device.Model : model;
        device.AndroidVersion = string.IsNullOrWhiteSpace(release) ? device.AndroidVersion : release;
        if (int.TryParse(sdkText, out var sdk))
            device.SdkLevel = sdk;

        var sizeMatch = rest.Select(l => SizeRegex.Match(l)).LastOrDefault(m => m.Success);
        if (sizeMatch is { Success: true })
            device.Resolution = sizeMatch.Groups[1].Value;

        var ipMatch = rest.Select(l => Ipv4Regex.Match(l)).FirstOrDefault(m => m.Success);
        if (ipMatch is { Success: true })
            device.WifiAddress = ipMatch.Groups[1].Value;

        return device;
    }

    public async Task<(bool Ok, string Message)> ConnectAsync(string address,
        CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return (false, "ADB no está instalado.");
        if (string.IsNullOrWhiteSpace(address))
            return (false, "Escribe la dirección IP y el puerto del teléfono.");

        var result = await ProcessRunner.RunAsync(AdbPath, $"connect \"{address.Trim()}\"",
            timeout: TimeSpan.FromSeconds(25), cancellationToken: cancellationToken);
        var text = result.Combined;
        var ok = result.Success &&
                 (text.Contains("connected to", StringComparison.OrdinalIgnoreCase) ||
                  text.Contains("already connected", StringComparison.OrdinalIgnoreCase));
        return (ok, text.Length == 0 ? (ok ? "Conectado." : "No se pudo conectar.") : text);
    }

    public async Task<(bool Ok, string Message)> PairAsync(string address, string code,
        CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return (false, "ADB no está instalado.");
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(code))
            return (false, "Escribe la dirección de emparejamiento y el código de 6 dígitos.");

        var result = await ProcessRunner.RunAsync(AdbPath, $"pair \"{address.Trim()}\" \"{code.Trim()}\"",
            timeout: TimeSpan.FromSeconds(40), cancellationToken: cancellationToken);
        var text = result.Combined;
        var ok = result.Success && text.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase);
        return (ok, text.Length == 0 ? (ok ? "Emparejado." : "No se pudo emparejar.") : text);
    }

    public async Task<(bool Ok, string Message)> DisconnectAsync(string? address,
        CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return (false, "ADB no está instalado.");

        var arguments = string.IsNullOrWhiteSpace(address) ? "disconnect" : $"disconnect \"{address.Trim()}\"";
        var result = await ProcessRunner.RunAsync(AdbPath, arguments, timeout: TimeSpan.FromSeconds(20),
            cancellationToken: cancellationToken);
        return (result.Success, result.Combined.Length > 0 ? result.Combined : "Desconectado.");
    }

    /// <summary>Switches a USB-connected phone to TCP/IP mode, then connects over the local network.</summary>
    public async Task<(bool Ok, string Message)> EnableWifiOverUsbAsync(string serial, int port = 5555,
        CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return (false, "ADB no está instalado.");

        var tcpip = await ProcessRunner.RunAsync(AdbPath, $"-s \"{serial}\" tcpip {port}",
            timeout: TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);
        if (!tcpip.Success)
            return (false, "No se pudo activar Wi-Fi por USB: " + tcpip.Combined);

        await Task.Delay(1200, cancellationToken);
        var ip = await GetWifiAddressAsync(serial, cancellationToken);
        if (string.IsNullOrWhiteSpace(ip))
            return (false, "Se activó el modo TCP/IP, pero no se pudo leer la IP del teléfono. " +
                           "Comprueba que el teléfono y la PC estén en la misma red Wi-Fi.");

        var address = $"{ip}:{port}";
        var connect = await ConnectAsync(address, cancellationToken);
        return connect.Ok
            ? (true, $"Wi-Fi activado. Conectado a {address}. Ya puedes desconectar el cable USB.")
            : (false, $"Se activó TCP/IP en {address}, pero la conexión falló: {connect.Message}");
    }

    public async Task<string?> GetWifiAddressAsync(string serial, CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return null;

        var result = await ProcessRunner.RunAsync(AdbPath,
            $"-s \"{serial}\" shell \"ip route; ip -f inet addr show wlan0\"",
            timeout: TimeSpan.FromSeconds(15), cancellationToken: cancellationToken);
        if (!result.Success)
            return null;

        foreach (var line in result.StdOut.Replace("\r", string.Empty).Split('\n'))
        {
            var match = Ipv4Regex.Match(line);
            if (match.Success && !line.Contains("127.0.0.1", StringComparison.Ordinal))
                return match.Groups[1].Value;
        }
        return null;
    }

    public async Task<string?> GetScreenSizeAsync(string serial, CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return null;
        var result = await ProcessRunner.RunAsync(AdbPath, $"-s \"{serial}\" shell wm size",
            timeout: TimeSpan.FromSeconds(15), cancellationToken: cancellationToken);
        if (!result.Success)
            return null;

        var matches = SizeRegex.Matches(result.StdOut);
        return matches.Count > 0 ? matches[^1].Groups[1].Value : null;
    }

    /// <summary>Captures a PNG screenshot through adb. Used for touch calibration and orientation-aware coordinates.</summary>
    public async Task<(int Width, int Height, byte[] Data)?> CaptureScreenshotAsync(string serial,
        CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return null;

        var startInfo = new ProcessStartInfo
        {
            FileName = AdbPath,
            Arguments = $"-s \"{serial}\" exec-out screencap -p",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return null;

            using var memory = new MemoryStream();
            var copyTask = process.StandardOutput.BaseStream.CopyToAsync(memory, cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await copyTask.WaitAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return null;
            }
            _ = await errorTask;

            var data = memory.ToArray();
            if (data.Length < 24)
                return null;
            // PNG signature + IHDR width/height (big-endian).
            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47)
                return null;
            var width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            var height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            if (width <= 0 || height <= 0)
                return null;
            return (width, height, data);
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo capturar la pantalla del teléfono: " + ex.Message);
            return null;
        }
    }

    public async Task<bool> SupportsMotionEventsAsync(string serial, CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return false;
        var result = await ProcessRunner.RunAsync(AdbPath, $"-s \"{serial}\" shell input",
            timeout: TimeSpan.FromSeconds(15), cancellationToken: cancellationToken);
        return result.Combined.Contains("motionevent", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<MdnsServiceInfo>> DiscoverMdnsAsync(CancellationToken cancellationToken = default)
    {
        var services = new List<MdnsServiceInfo>();
        if (AdbPath is null)
            return services;

        var result = await ProcessRunner.RunAsync(AdbPath, "mdns services", timeout: TimeSpan.FromSeconds(15),
            cancellationToken: cancellationToken);
        if (!result.Success)
            return services;

        foreach (var rawLine in result.StdOut.Replace("\r", string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("List of discovered", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            var name = parts[0];
            var address = parts[1];
            if (!address.Contains(':'))
                continue;

            services.Add(new MdnsServiceInfo
            {
                Name = name,
                Address = address,
                ServiceType = name
            });
        }

        return services;
    }

    public async Task<string> ShellAsync(string serial, string command,
        CancellationToken cancellationToken = default)
    {
        if (AdbPath is null)
            return string.Empty;
        var result = await ProcessRunner.RunAsync(AdbPath, $"-s \"{serial}\" shell \"{command}\"",
            timeout: TimeSpan.FromSeconds(20), cancellationToken: cancellationToken);
        return result.StdOut;
    }

    public async Task<(bool Ok, string Message)> InstallPlatformToolsAsync(
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        const string url = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";
        var zip = Path.Combine(Paths.DownloadsDirectory, "platform-tools-latest-windows.zip");

        try
        {
            progress?.Report(new DownloadProgress(0, null));

            await DownloadService.DownloadAsync(url, zip, progress, cancellationToken);

            var temp = Path.Combine(Paths.DownloadsDirectory, "platform-tools-extract-" + Guid.NewGuid().ToString("N"));
            try
            {
                DownloadService.ExtractFolder(zip, "platform-tools", temp);
                if (!DownloadService.CopyDirectory(temp, Paths.PlatformToolsDirectory, overwrite: true))
                {
                    // Some binaries were locked by a running ADB server: stop it and retry.
                    await StopAdbServerAsync(cancellationToken);
                    DownloadService.CopyDirectory(temp, Paths.PlatformToolsDirectory, overwrite: true);
                }
            }
            finally
            {
                try { Directory.Delete(temp, recursive: true); } catch { /* ignore */ }
            }

            foreach (var file in new[] { "adb.exe", "AdbWinApi.dll", "AdbWinUsbApi.dll" })
            {
                var path = Path.Combine(Paths.PlatformToolsDirectory, file);
                if (!File.Exists(path))
                    return (false, $"La descarga oficial no contiene '{file}'.");
            }

            _adbPath = Path.Combine(Paths.PlatformToolsDirectory, "adb.exe");
            var status = await GetStatusAsync(cancellationToken);
            return status.IsAvailable
                ? (true, $"ADB instalado: {status.Version}")
                : (false, status.Detail);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo instalar platform-tools.");
            return (false, "No se pudo descargar platform-tools oficial: " + ex.Message);
        }
    }

    /// <summary>Stops the ADB server so its executables are not locked.</summary>
    private async Task StopAdbServerAsync(CancellationToken cancellationToken)
    {
        var path = AdbPath;
        if (path is null || !File.Exists(path))
            return;

        try
        {
            await ProcessRunner.RunAsync(path, "kill-server", timeout: TimeSpan.FromSeconds(15),
                cancellationToken: cancellationToken);
            await Task.Delay(800, cancellationToken);
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo detener el servidor ADB: " + ex.Message);
        }
    }

    /// <summary>Starts an interactive adb shell used to inject touch events with minimal overhead.</summary>
    public AdbShellSession? StartShellSession(string serial)
    {
        if (AdbPath is null)
            return null;
        try
        {
            var session = new AdbShellSession(AdbPath, serial);
            session.Start();
            return session;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo abrir una sesión de shell ADB.");
            return null;
        }
    }

    public static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Any(p =>
            {
                try
                {
                    return !p.HasExited;
                }
                finally
                {
                    p.Dispose();
                }
            });
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Persistent `adb shell` process. Writes commands to stdin without spawning adb per event.</summary>
public sealed class AdbShellSession : IDisposable
{
    private readonly string _adbPath;
    private readonly string _serial;
    private readonly object _gate = new();
    private Process? _process;
    private bool _disposed;

    public AdbShellSession(string adbPath, string serial)
    {
        _adbPath = adbPath;
        _serial = serial;
    }

    public bool IsAlive
    {
        get
        {
            lock (_gate)
                return _process is { HasExited: false };
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AdbShellSession));

            _process?.Dispose();
            var startInfo = new ProcessStartInfo
            {
                FileName = _adbPath,
                Arguments = $"-s \"{_serial}\" shell",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _process = Process.Start(startInfo) ?? throw new InvalidOperationException("adb shell no inició.");
            _process.OutputDataReceived += (_, _) => { };
            _process.ErrorDataReceived += (_, _) => { };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
    }

    public bool TryWriteLine(string command)
    {
        lock (_gate)
        {
            if (_disposed || _process is not { HasExited: false })
                return false;

            try
            {
                _process.StandardInput.WriteLine(command);
                _process.StandardInput.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                if (_process is { HasExited: false })
                    _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }
            _process?.Dispose();
            _process = null;
        }
    }
}
