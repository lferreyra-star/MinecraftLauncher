using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.Services;

public sealed class ModDownloadService
{
    private const string CurseForgeApi = "https://api.curseforge.com/v1/mods";
    private const string ModrinthApi = "https://api.modrinth.com/v2/mods";

    private readonly HttpClient _http;

    public ModDownloadService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task DownloadModsAsync(string versionId, JavaWorkspace workspace, JavaProfile profile, IProgress<JavaInstallProgress> progress, CancellationToken cancellationToken)
    {
        progress?.Report(new JavaInstallProgress
        {
            Stage = "Buscando mods compatibles",
            Detail = $"Version {versionId}",
            Current = 0,
            Total = 1
        });

        var manifestPath = Path.Combine(workspace.VersionsDirectory, versionId, versionId + ".json");
        if (!File.Exists(manifestPath))
        {
            progress?.Report(new JavaInstallProgress
            {
                Stage = "Error",
                Detail = "No se encontró el manifiesto de la versión",
                Current = 0,
                Total = 1
            });
            throw new InvalidOperationException("No se encontró el manifiesto de la versión.");
        }

        var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        using var manifestDocument = JsonDocument.Parse(manifestJson);
        var root = manifestDocument.RootElement;

        string? loader = null;
        if (root.TryGetProperty("", out var loaderProp))
        {
            loader = loaderProp.GetString();
        }

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Analizando cargador",
            Detail = loader ?? "Desconocido",
            Current = 1,
            Total = 1
        });

        // Determinar qué mods son compatibles según el cargador
        var mods = loader switch
        {
            "fabric" => await GetFabricModsAsync(versionId, progress, cancellationToken),
            _ => await GetVanillaModsAsync(versionId, progress, cancellationToken)
        };

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Descargando mods",
            Detail = $"({mods.Count} mods)",
            Current = 0,
            Total = mods.Count
        });

        var modsDirectory = workspace.GetModsDirectory(profile);
        Directory.CreateDirectory(modsDirectory);

        for (int i = 0; i < mods.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mod = mods[i];
            var modFilePath = Path.Combine(modsDirectory, mod.FileName);

            if (File.Exists(modFilePath))
            {
                progress?.Report(new JavaInstallProgress
                {
                    Stage = "Descargando mods",
                    Detail = $"{mod.FileName} (ya existe)",
                    Current = i + 1,
                    Total = mods.Count
                });
                continue;
            }

            progress?.Report(new JavaInstallProgress
            {
                Stage = "Descargando mods",
                Detail = mod.FileName,
                Current = i,
                Total = mods.Count
            });

            Directory.CreateDirectory(Path.GetDirectoryName(modFilePath)!);
            await DownloadFileAsync(mod.DownloadUrl, modFilePath, cancellationToken);

            progress?.Report(new JavaInstallProgress
            {
                Stage = "Descargando mods",
                Detail = $"{mod.FileName} completado",
                Current = i + 1,
                Total = mods.Count
            });
        }

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Mods listos",
            Detail = $"Se descargaron {mods.Count} mods",
            Current = 1,
            Total = 1
        });
    }

    private async Task<List<ModEntry>> GetFabricModsAsync(string versionId, IProgress<JavaInstallProgress> progress, CancellationToken cancellationToken)
    {
        // Fabric mods - consultar el catálogo de Fabric
        var listJson = await _http.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{versionId}");
        var entries = JsonNode.Parse(listJson)?.AsArray();

        var modList = new List<ModEntry>();

        if (entries is null)
            return modList;

        foreach (var entry in entries)
        {
            var version = entry?["loader"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(version))
                continue;

            // Agregar mods básicos de Fabric compatibles
            modList.Add(new ModEntry
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Fabric Loader ${version}",
                FileName = $"fabric-loader-{version}.jar",
                DownloadUrl = $"https://maven.fabricmc.net/net/fabricmc/fabric-loader/{version}/fabric-loader-{version}.jar"
            });
        }

        return modList;
    }

    private async Task<List<ModEntry>> GetVanillaModsAsync(string versionId, IProgress<JavaInstallProgress> progress, CancellationToken cancellationToken)
    {
        // Para versiones vanilla, retornar mods populares básicos
        var modList = new List<ModEntry>
        {
            new ModEntry
            {
                Id = "1",
                Name = "OptiFine",
                FileName = $"OptiFine-{versionId}.jar",
                DownloadUrl = "https://optifine.net/downloads/latest/optifine.jar"
            },
            new ModEntry
            {
                Id = "2",
                Name = "JourneyMap",
                FileName = $"journeymap-{versionId}.jar",
                DownloadUrl = "https://telecraft.org/journeymap/download"
            }
        };

        return modList;
    }

    private async Task DownloadFileAsync(string url, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (File.Exists(destination))
        {
            var length = new FileInfo(destination).Length;
            // Simple check - en producción se compararía el tamaño
            if (length > 0)
                return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = File.Create(destination);
        await stream.CopyToAsync(fileStream);
    }
}

public sealed class ModEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FileName { get; init; }
    public required string DownloadUrl { get; init; }
}
