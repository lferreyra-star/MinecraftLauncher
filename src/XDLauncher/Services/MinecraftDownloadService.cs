using System.Collections.ObjectModel;
using System.Net.Http;
using XdLauncher.Core;
using XdLauncher.Models;

namespace XdLauncher.Services;

/// <summary>Gestiona la lista de descargas disponibles y el estado de cada una.</summary>
public sealed class MinecraftDownloadService : ObservableObject
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private readonly Dictionary<string, CancellationTokenSource> _activeDownloads = new();
    private readonly ObservableCollection<DownloadItem> _items = [];

    public MinecraftDownloadService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "XDLauncher/1.2 (+https://www.minecraft.net)");

        InitializeItems();
    }

    public IReadOnlyList<DownloadItem> Items => _items;

    public DownloadItem? GetItem(string key) => _items.FirstOrDefault(i => i.Key == key);

    public bool IsAnyDownloading => _items.Any(i => i.State == DownloadState.Downloading);

    private void InitializeItems()
    {
        _items.Add(new DownloadItem
        {
            Key = "minecraft-retail",
            Title = "Minecraft: Bedrock Edition (Retail)",
            Description = "Versión estable oficial de Minecraft Bedrock para Windows 10/11.",
            DownloadUrl = AppInfo.StoreWebUrl,
            StoreUrl = AppInfo.StoreWebUrl,
            FileName = "Minecraft-Retail.msixbundle",
            Category = "Minecraft",
            IconGlyph = "\uE7FC",
            SizeLabel = "Vía Microsoft Store"
        });

        _items.Add(new DownloadItem
        {
            Key = "minecraft-preview",
            Title = "Minecraft Preview",
            Description = "Canal de pruebas con funciones nuevas antes que en la versión estable.",
            DownloadUrl = "https://apps.microsoft.com/detail/9MVQ3DQXJQXS",
            StoreUrl = "https://apps.microsoft.com/detail/9MVQ3DQXJQXS",
            FileName = "Minecraft-Preview.msixbundle",
            Category = "Minecraft",
            IconGlyph = "\uE7C0",
            SizeLabel = "Vía Microsoft Store"
        });

        _items.Add(new DownloadItem
        {
            Key = "adb-platform-tools",
            Title = "Android Platform-Tools (ADB)",
            Description = "Herramientas oficiales de Google para depuración USB/Wi-Fi (usado por Mobile Connect).",
            DownloadUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip",
            StoreUrl = string.Empty,
            FileName = "platform-tools-latest-windows.zip",
            Category = "Herramientas",
            IconGlyph = "\uE756",
            SizeLabel = "~30 MB"
        });

        _items.Add(new DownloadItem
        {
            Key = "scrcpy",
            Title = "scrcpy (transmisión móvil)",
            Description = "Herramienta oficial de Genymobile para ver y controlar el móvil desde el PC. Se instala y verifica automáticamente.",
            DownloadUrl = "https://api.github.com/repos/Genymobile/scrcpy/releases/latest",
            StoreUrl = string.Empty,
            FileName = "scrcpy-win64.zip",
            Category = "Herramientas",
            IconGlyph = "\uE12C",
            SizeLabel = "~15 MB"
        });
    }

    public async Task StartDownloadAsync(DownloadItem item, IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (item.State is DownloadState.Downloading or DownloadState.Queued)
            return;

        if (item.IsMinecraft)
        {
            item.SetState(DownloadState.ReadyToInstall);
            item.StatusMessage = "Abriendo Microsoft Store…";
            FileOps.OpenUrl(item.StoreUrl);
            OnPropertyChanged(nameof(IsAnyDownloading));
            return;
        }

        if (item.Key == "scrcpy")
        {
            await InstallScrcpyAsync(item, progress, cancellationToken);
            return;
        }

        var cts = new CancellationTokenSource();
        _activeDownloads[item.Key] = cts;
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        item.SetState(DownloadState.Downloading);
        item.IsIndeterminate = true;
        item.StatusMessage = $"Descargando {item.Title}…";
        OnPropertyChanged(nameof(IsAnyDownloading));

        try
        {
            var destination = Path.Combine(Paths.DownloadsDirectory, item.FileName);
            var progressReporter = new Progress<DownloadProgress>(p =>
            {
                item.UpdateProgress(p.BytesReceived, p.TotalBytes);
                progress?.Report(p);
            });

            await DownloadService.DownloadAsync(item.DownloadUrl, destination, progressReporter, linked.Token);

            if (linked.Token.IsCancellationRequested)
            {
                item.MarkCancelled();
                return;
            }

            item.MarkCompleted();
            ToastHub.Success($"{item.Title} descargado correctamente.");
        }
        catch (OperationCanceledException)
        {
            item.MarkCancelled();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"No se pudo descargar {item.Key}.");
            item.MarkFailed(ex.Message);
            ToastHub.Error($"No se pudo descargar {item.Title}.");
        }
        finally
        {
            _activeDownloads.Remove(item.Key);
            cts.Dispose();
            OnPropertyChanged(nameof(IsAnyDownloading));
        }
    }

    private async Task InstallScrcpyAsync(DownloadItem item, IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var cts = new CancellationTokenSource();
        _activeDownloads[item.Key] = cts;
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        item.SetState(DownloadState.Downloading);
        item.IsIndeterminate = true;
        item.StatusMessage = "Consultando la última versión oficial de scrcpy…";
        OnPropertyChanged(nameof(IsAnyDownloading));

        try
        {
            var scrcpy = new ScrcpyService();
            var reporter = new Progress<DownloadProgress>(p =>
            {
                item.UpdateProgress(p.BytesReceived, p.TotalBytes);
                progress?.Report(p);
            });

            var (ok, message) = await scrcpy.InstallLatestAsync(reporter, linked.Token);
            if (linked.Token.IsCancellationRequested)
            {
                item.MarkCancelled();
                return;
            }

            if (ok)
            {
                item.MarkCompleted();
                ToastHub.Success(message);
            }
            else
            {
                item.MarkFailed(message);
                ToastHub.Warning(message);
            }
        }
        catch (OperationCanceledException)
        {
            item.MarkCancelled();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo instalar scrcpy desde la interfaz de descargas.");
            item.MarkFailed(ex.Message);
        }
        finally
        {
            _activeDownloads.Remove(item.Key);
            cts.Dispose();
            OnPropertyChanged(nameof(IsAnyDownloading));
        }
    }

    public Task CancelDownloadAsync(DownloadItem item)
    {
        if (_activeDownloads.TryGetValue(item.Key, out var cts))
        {
            cts.Cancel();
            item.StatusMessage = "Cancelando descarga…";
        }
        else
        {
            item.MarkCancelled();
        }
        return Task.CompletedTask;
    }

    public void RetryDownload(DownloadItem item)
    {
        item.Reset();
        _ = StartDownloadAsync(item);
    }

    public void OpenStore(DownloadItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.StoreUrl))
            FileOps.OpenUrl(item.StoreUrl);
    }

    public void OpenDownloadedFile(DownloadItem item)
    {
        var path = Path.Combine(Paths.DownloadsDirectory, item.FileName);
        if (File.Exists(path))
            FileOps.ShowInExplorer(path);
        else if (item.Key == "scrcpy" && ScrcpyService.FindExecutable() is { } exe)
            FileOps.ShowInExplorer(exe);
        else
            ToastHub.Warning("El archivo descargado no se encontró.");
    }

    public void Dispose()
    {
        foreach (var cts in _activeDownloads.Values)
            cts.Cancel();
        _activeDownloads.Clear();
        _http.Dispose();
    }
}
