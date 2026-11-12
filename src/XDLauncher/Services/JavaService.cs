using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using XdLauncher.Models;

namespace XdLauncher.Services;

/// <summary>
/// Minecraft Java Edition: manifiesto oficial de Mojang, instalación de versiones vanilla,
/// detección/instalación de Java y lanzamiento real del cliente.
/// Todas las descargas provienen de servidores oficiales de Mojang.
/// </summary>
public sealed class JavaService
{
    public const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private const string RuntimeManifestUrl =
        "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
    private const string LibrariesRoot = "https://libraries.minecraft.net/";
    private const string AssetsRoot = "https://resources.download.minecraft.net/";
    private const string FabricMetaRoot = "https://meta.fabricmc.net/v2/versions/loader/";

    private static readonly Regex JavaVersionRegex =
        new("version\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly Dictionary<string, Process> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly JavaWorkspace _workspace;
    private JavaVersionManifest? _manifest;

    public event Action? RunningChanged;

    public JavaService(JavaWorkspace workspace)
    {
        _workspace = workspace;
        _workspace.EnsureDirectories();
    }

    public JavaWorkspace Workspace => _workspace;
    public string ManifestCachePath => _workspace.CacheManifestFile;
    public static string FabricVersionId(string baseVersionId) => baseVersionId + "-fabric";

    public bool IsInstalled(string versionId)
    {
        var directory = Path.Combine(_workspace.VersionsDirectory, versionId);
        return File.Exists(Path.Combine(directory, versionId + ".json")) &&
               File.Exists(Path.Combine(directory, versionId + ".jar"));
    }

    public IReadOnlyList<string> GetInstalledVersionIds()
    {
        try
        {
            if (!Directory.Exists(_workspace.VersionsDirectory))
                return [];

            return Directory.EnumerateDirectories(_workspace.VersionsDirectory)
                .Select(Path.GetFileName)
                .Where(id => id is { Length: > 0 } && IsInstalled(id!))
                .Select(id => id!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Elimina los archivos de una versión y de su variante Fabric solo en este workspace.</summary>
    public Task DeleteVersionFilesAsync(string versionId)
    {
        foreach (var id in new[] { versionId, FabricVersionId(versionId) })
        {
            var directory = Path.Combine(_workspace.VersionsDirectory, id);
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex)
            {
                LogService.Warn($"No se pudo eliminar la versión {id}: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Mayor de Java que exige una versión instalada (17 si no se puede leer).</summary>
    public int GetRequiredJavaMajor(string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId))
            return 17;

        var jsonPath = Path.Combine(_workspace.VersionsDirectory, versionId, versionId + ".json");
        try
        {
            if (!File.Exists(jsonPath))
                return 17;

            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            if (document.RootElement.TryGetProperty("javaVersion", out var javaVersion) &&
                javaVersion.TryGetProperty("majorVersion", out var major) &&
                major.TryGetInt32(out var value))
                return value;
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo leer el Java requerido: " + ex.Message);
        }

        return 17;
    }

    public async Task<JavaVersionManifest> GetManifestAsync(bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (!force && _manifest is not null)
            return _manifest;

        string? json = null;
        try
        {
            json = await DownloadService.TryDownloadTextAsync(ManifestUrl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                Directory.CreateDirectory(_workspace.Root);
                await File.WriteAllTextAsync(ManifestCachePath, json, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo consultar el manifiesto de Java: " + ex.Message);
        }

        if (string.IsNullOrWhiteSpace(json) && File.Exists(ManifestCachePath))
            json = await File.ReadAllTextAsync(ManifestCachePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(
                "No se pudo consultar el manifiesto oficial de Minecraft. Comprueba tu conexión.");

        _manifest = ParseManifest(json);
        return _manifest;
    }

    private static JavaVersionManifest ParseManifest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var versions = new List<JavaVersionEntry>();

        if (root.TryGetProperty("versions", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var version in array.EnumerateArray())
            {
                var id = GetString(version, "id");
                var url = GetString(version, "url");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url))
                    continue;

                DateTimeOffset releaseTime = DateTimeOffset.MinValue;
                var release = GetString(version, "releaseTime");
                if (!string.IsNullOrWhiteSpace(release))
                    DateTimeOffset.TryParse(release, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out releaseTime);

                versions.Add(new JavaVersionEntry
                {
                    Id = id,
                    Type = GetString(version, "type") ?? "release",
                    Url = url,
                    ReleaseTime = releaseTime,
                    Sha1 = GetString(version, "sha1") ?? string.Empty
                });
            }
        }

        var latest = root.TryGetProperty("latest", out var latestElement) ? latestElement : default;
        return new JavaVersionManifest
        {
            LatestRelease = latest.ValueKind == JsonValueKind.Object ? GetString(latest, "release") : null,
            LatestSnapshot = latest.ValueKind == JsonValueKind.Object ? GetString(latest, "snapshot") : null,
            Versions = versions
        };
    }

    public async Task InstallAsync(string versionId, IProgress<JavaInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var versionDirectory = Path.Combine(_workspace.VersionsDirectory, versionId);
        Directory.CreateDirectory(versionDirectory);
        var jsonPath = Path.Combine(versionDirectory, versionId + ".json");

        if (!File.Exists(jsonPath) && _manifest is null)
            await GetManifestAsync(cancellationToken: cancellationToken);

        var entry = _manifest?.Versions.FirstOrDefault(v =>
            v.Id.Equals(versionId, StringComparison.OrdinalIgnoreCase));

        if (!File.Exists(jsonPath))
        {
            if (entry is null)
                throw new InvalidOperationException("Versión desconocida en el manifiesto oficial.");

            progress?.Report(new JavaInstallProgress
            {
                Stage = "Descargando manifiesto de la versión",
                Detail = versionId
            });
            await DownloadService.DownloadAsync(entry.Url, jsonPath, null, cancellationToken);
        }

        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Client jar
        var jarPath = Path.Combine(versionDirectory, versionId + ".jar");
        if (root.TryGetProperty("downloads", out var downloadsElement) &&
            downloadsElement.TryGetProperty("client", out var clientElement))
        {
            var clientUrl = GetString(clientElement, "url");
            var clientSize = GetLong(clientElement, "size");
            if (!string.IsNullOrWhiteSpace(clientUrl))
            {
                progress?.Report(new JavaInstallProgress
                {
                    Stage = "Descargando cliente",
                    Detail = versionId,
                    Current = 0,
                    Total = 1
                });
                await DownloadIfMissingAsync(clientUrl, jarPath, clientSize, null, cancellationToken);
            }
        }

        if (!File.Exists(jarPath))
            throw new InvalidOperationException("La versión no incluye un cliente válido para Windows.");

        // Libraries and natives
        var libraries = new List<LibraryFile>();
        var natives = new List<LibraryFile>();
        if (root.TryGetProperty("libraries", out var librariesElement) &&
            librariesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var library in librariesElement.EnumerateArray())
            {
                if (!IsAllowed(library))
                    continue;

                JsonElement? classifierRoot = null;
                if (library.TryGetProperty("downloads", out var downloads) &&
                    downloads.ValueKind == JsonValueKind.Object)
                    classifierRoot = downloads;

                if (classifierRoot is { } downloadsValue)
                {
                    var libraryName = GetString(library, "name") ?? string.Empty;
                    var extractedNative = false;

                    if (downloadsValue.TryGetProperty("artifact", out var artifact))
                    {
                        var path = GetString(artifact, "path");
                        var url = GetString(artifact, "url");
                        if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(url))
                        {
                            var file = new LibraryFile(path!, url!,
                                GetString(artifact, "sha1"), GetLong(artifact, "size"));
                            if (IsWindowsNative(libraryName, path))
                            {
                                natives.Add(file);
                                extractedNative = true;
                            }
                            else if (!IsForeignNative(libraryName, path))
                            {
                                libraries.Add(file);
                            }
                        }
                    }

                    if (!extractedNative &&
                        downloadsValue.TryGetProperty("classifiers", out var classifiers) &&
                        classifiers.ValueKind == JsonValueKind.Object)
                    {
                        var nativeKey = library.TryGetProperty("natives", out var nativesElement) &&
                                        nativesElement.ValueKind == JsonValueKind.Object
                            ? GetString(nativesElement, "windows")
                            : null;
                        if (!string.IsNullOrWhiteSpace(nativeKey))
                        {
                            var classifierName = nativeKey!.Replace("${arch}", "64");
                            if (classifiers.TryGetProperty(classifierName, out var nativeArtifact))
                            {
                                var path = GetString(nativeArtifact, "path");
                                var url = GetString(nativeArtifact, "url");
                                if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(url))
                                    natives.Add(new LibraryFile(path!, url!,
                                        GetString(nativeArtifact, "sha1"), GetLong(nativeArtifact, "size")));
                            }
                        }
                    }
                }
                else
                {
                    var name = GetString(library, "name");
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var path = MavenPath(name!);
                    if (IsForeignNative(name, path))
                        continue;

                    var baseUrl = GetString(library, "url");
                    if (string.IsNullOrWhiteSpace(baseUrl))
                        baseUrl = LibrariesRoot;
                    baseUrl = baseUrl!.TrimEnd('/') + "/";

                    var file = new LibraryFile(path, baseUrl + path, null, 0);
                    if (IsWindowsNative(name, path))
                        natives.Add(file);
                    else
                        libraries.Add(file);
                }
            }
        }

        var allLibraries = libraries.Concat(natives).ToList();
        for (var index = 0; index < allLibraries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var library = allLibraries[index];
            progress?.Report(new JavaInstallProgress
            {
                Stage = "Descargando bibliotecas",
                Detail = Path.GetFileName(library.Path),
                Current = index + 1,
                Total = allLibraries.Count
            });

            var destination = Path.Combine(_workspace.LibrariesDirectory, library.Path);
            await DownloadIfMissingAsync(library.Url, destination, library.Size, library.Sha1, cancellationToken);
        }

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Extrayendo bibliotecas nativas",
            Detail = versionId,
            Current = 0,
            Total = 1
        });
        ExtractNatives(versionId, natives);

        // Assets
        await InstallAssetsAsync(root, progress, cancellationToken);

        // Logging configuration
        await InstallLoggingAsync(root, versionDirectory, cancellationToken);

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Versión lista",
            Detail = versionId,
            Current = 1,
            Total = 1
        });
    }

    /// <summary>
    /// Creates a standalone Fabric-enabled version in this workspace by merging the official
    /// vanilla JSON with Fabric's loader profile. Libraries are downloaded from their declared
    /// Maven repositories. No authentication data is involved.
    /// </summary>
    public async Task<string?> InstallFabricAsync(string baseVersionId,
        IProgress<JavaInstallProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var derivedId = FabricVersionId(baseVersionId);
        var baseDirectory = Path.Combine(_workspace.VersionsDirectory, baseVersionId);
        var baseJsonPath = Path.Combine(baseDirectory, baseVersionId + ".json");
        var baseJarPath = Path.Combine(baseDirectory, baseVersionId + ".jar");
        if (!File.Exists(baseJsonPath) || !File.Exists(baseJarPath))
            throw new InvalidOperationException("Instala primero la versión vanilla en este workspace.");

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Consultando Fabric Meta",
            Detail = baseVersionId,
            Current = 0,
            Total = 1
        });

        var listJson = await DownloadService.TryDownloadTextAsync(FabricMetaRoot + baseVersionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(listJson))
            throw new InvalidOperationException("No se pudo consultar el catálogo de Fabric para esta versión.");

        string? loaderVersion = null;
        string? fallbackVersion = null;
        try
        {
            var entries = JsonNode.Parse(listJson)?.AsArray();
            if (entries is null || entries.Count == 0)
                throw new InvalidOperationException("Fabric no publica un cargador para esta versión.");

            foreach (var entry in entries)
            {
                var loader = entry?["loader"] as JsonObject;
                var candidate = loader?["version"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (fallbackVersion is null)
                    fallbackVersion = candidate;

                if (loader?["stable"]?.GetValue<bool>() == true)
                {
                    loaderVersion = candidate;
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("El catálogo de Fabric no tiene un formato válido: " + ex.Message);
        }

        loaderVersion ??= fallbackVersion;
        if (string.IsNullOrWhiteSpace(loaderVersion))
            throw new InvalidOperationException("Fabric no indica una versión de cargador.");

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Descargando el perfil de Fabric",
            Detail = loaderVersion,
            Current = 0,
            Total = 1
        });

        var profileUrl = FabricMetaRoot + Uri.EscapeDataString(baseVersionId) + "/" +
                         Uri.EscapeDataString(loaderVersion) + "/profile/json";
        var profileJson = await DownloadService.TryDownloadTextAsync(profileUrl, cancellationToken);
        if (string.IsNullOrWhiteSpace(profileJson))
            throw new InvalidOperationException("No se pudo descargar el perfil de Fabric.");

        try
        {
            var fabricProfile = JsonNode.Parse(profileJson)?.AsObject();
            var baseVersion = JsonNode.Parse(await File.ReadAllTextAsync(baseJsonPath, cancellationToken))?.AsObject();
            if (fabricProfile is null || baseVersion is null)
                throw new InvalidOperationException("El perfil de Fabric o la versión base no son JSON válido.");

            var merged = (JsonObject)baseVersion.DeepClone();
            merged.Remove("inheritsFrom");
            merged["id"] = derivedId;

            var mainClass = fabricProfile["mainClass"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(mainClass))
                merged["mainClass"] = mainClass;

            var fabricLibraries = fabricProfile["libraries"] as JsonArray;
            if (fabricLibraries is not null)
            {
                var libraries = merged["libraries"] as JsonArray ?? new JsonArray();
                foreach (var library in fabricLibraries)
                {
                    var libraryObject = library as JsonObject;
                    var name = libraryObject?["name"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var existingIndex = -1;
                    for (var index = 0; index < libraries.Count; index++)
                    {
                        if (string.Equals(libraries[index]?["name"]?.GetValue<string>(), name,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            existingIndex = index;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                        libraries[existingIndex] = libraryObject!.DeepClone();
                    else
                        libraries.Add(libraryObject!.DeepClone());
                }

                merged["libraries"] = libraries;
            }

            if (fabricProfile["arguments"] is JsonObject fabricArguments)
            {
                var mergedArguments = merged["arguments"] as JsonObject ?? new JsonObject();
                foreach (var key in new[] { "jvm", "game" })
                {
                    if (fabricArguments[key] is not JsonArray fabricArray)
                        continue;

                    var combined = new JsonArray();
                    if (mergedArguments[key] is JsonArray existingArray)
                    {
                        foreach (var value in existingArray)
                            combined.Add(value?.DeepClone());
                    }

                    foreach (var value in fabricArray)
                        combined.Add(value?.DeepClone());

                    mergedArguments[key] = combined;
                }

                merged["arguments"] = mergedArguments;
            }

            var derivedDirectory = Path.Combine(_workspace.VersionsDirectory, derivedId);
            Directory.CreateDirectory(derivedDirectory);
            var derivedJsonPath = Path.Combine(derivedDirectory, derivedId + ".json");
            await File.WriteAllTextAsync(derivedJsonPath,
                merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            File.Copy(baseJarPath, Path.Combine(derivedDirectory, derivedId + ".jar"), overwrite: true);

            progress?.Report(new JavaInstallProgress
            {
                Stage = "Instalando bibliotecas de Fabric",
                Detail = loaderVersion,
                Current = 0,
                Total = 1
            });

            await InstallAsync(derivedId, progress, cancellationToken);
            return loaderVersion;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("No se pudo combinar la versión con Fabric: " + ex.Message, ex);
        }
    }

    private async Task InstallAssetsAsync(JsonElement root, IProgress<JavaInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("assetIndex", out var assetIndex) ||
            assetIndex.ValueKind != JsonValueKind.Object)
            return;

        var id = GetString(assetIndex, "id");
        var url = GetString(assetIndex, "url");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url))
            return;

        var indexDirectory = Path.Combine(_workspace.AssetsDirectory, "indexes");
        Directory.CreateDirectory(indexDirectory);
        var indexPath = Path.Combine(indexDirectory, id + ".json");
        await DownloadIfMissingAsync(url!, indexPath, GetLong(assetIndex, "size"), null, cancellationToken);

        List<(string Name, string Hash, long Size)> objects = [];
        var virtualAssets = false;
        var mapToResources = false;
        try
        {
            using var indexStream = File.OpenRead(indexPath);
            using var indexDocument = await JsonDocument.ParseAsync(
                indexStream, cancellationToken: cancellationToken);
            var indexRoot = indexDocument.RootElement;
            virtualAssets = indexRoot.TryGetProperty("virtual", out var virtualElement) &&
                            virtualElement.ValueKind == JsonValueKind.True;
            mapToResources = indexRoot.TryGetProperty("map_to_resources", out var mapElement) &&
                             mapElement.ValueKind == JsonValueKind.True;

            if (indexRoot.TryGetProperty("objects", out var objectsElement) &&
                objectsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in objectsElement.EnumerateObject())
                {
                    var hash = GetString(property.Value, "hash");
                    var size = GetLong(property.Value, "size");
                    if (!string.IsNullOrWhiteSpace(hash))
                        objects.Add((property.Name, hash!, size));
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo leer el índice de recursos: " + ex.Message);
            return;
        }

        var unique = objects.DistinctBy(o => o.Hash).ToList();
        var done = 0;
        var total = unique.Count;
        progress?.Report(new JavaInstallProgress
        {
            Stage = "Descargando recursos",
            Detail = id,
            Current = 0,
            Total = total
        });

        await Parallel.ForEachAsync(unique, new ParallelOptions
        {
            MaxDegreeOfParallelism = 8,
            CancellationToken = cancellationToken
        }, async (asset, token) =>
        {
            var directory = asset.Hash[..2];
            var destination = Path.Combine(_workspace.AssetsDirectory, "objects", directory, asset.Hash);
            if (File.Exists(destination) && (asset.Size <= 0 || new FileInfo(destination).Length == asset.Size))
            {
                Interlocked.Increment(ref done);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await DownloadService.DownloadAsync($"{AssetsRoot}{directory}/{asset.Hash}", destination, null, token);
            var current = Interlocked.Increment(ref done);
            if (current % 25 == 0 || current == total)
            {
                progress?.Report(new JavaInstallProgress
                {
                    Stage = "Descargando recursos",
                    Detail = $"{current} de {total}",
                    Current = current,
                    Total = total
                });
            }
        });

        if (!virtualAssets && !mapToResources)
            return;

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Preparando recursos heredados",
            Detail = id,
            Current = 0,
            Total = objects.Count
        });

        var virtualRoot = Path.Combine(_workspace.AssetsDirectory, "virtual", id);
        var resourcesRoot = Path.Combine(_workspace.DefaultGameDirectory, "resources");
        foreach (var asset in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.Combine(_workspace.AssetsDirectory, "objects", asset.Hash[..2], asset.Hash);
            if (!File.Exists(source))
                continue;

            if (virtualAssets)
                CopyIntoDirectory(source, virtualRoot, asset.Name);
            if (mapToResources)
                CopyIntoDirectory(source, resourcesRoot, asset.Name);
        }
    }

    private static void CopyIntoDirectory(string source, string root, string relativeName)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root);
            var rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(Path.Combine(fullRoot,
                relativeName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target) || new FileInfo(target).Length != new FileInfo(source).Length)
                File.Copy(source, target, overwrite: true);
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo preparar un recurso heredado: " + ex.Message);
        }
    }

    private static async Task InstallLoggingAsync(JsonElement root, string versionDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!root.TryGetProperty("logging", out var logging) ||
                !logging.TryGetProperty("client", out var client) ||
                !client.TryGetProperty("file", out var file))
                return;

            var id = GetString(file, "id");
            var url = GetString(file, "url");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url))
                return;

            var directory = Path.Combine(versionDirectory, "logging");
            Directory.CreateDirectory(directory);
            await DownloadIfMissingAsync(url!, Path.Combine(directory, id!), 0, null, cancellationToken);
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo descargar la configuración de registro: " + ex.Message);
        }
    }

    private static async Task DownloadIfMissingAsync(string url, string destination, long size, string? sha1,
        CancellationToken cancellationToken)
    {
        _ = sha1;
        if (File.Exists(destination))
        {
            var length = new FileInfo(destination).Length;
            if (size <= 0 || length == size)
                return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await DownloadService.DownloadAsync(url, destination, null, cancellationToken);
    }

    private static string MavenPath(string name)
    {
        var parts = name.Split(':');
        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length > 3 ? "-" + parts[3] : string.Empty;
        return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
    }

    private static bool IsWindowsNative(string? name, string? path)
    {
        var fileName = path is null ? string.Empty : Path.GetFileName(path);
        var text = (name ?? string.Empty) + " " + fileName;
        if (text.Contains("arm64", StringComparison.OrdinalIgnoreCase))
            return false;
        return text.Contains("natives-windows", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForeignNative(string? name, string? path)
    {
        var fileName = path is null ? string.Empty : Path.GetFileName(path);
        var text = (name ?? string.Empty) + " " + fileName;
        return text.Contains("natives-linux", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("natives-macos", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("natives-osx", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("natives-windows-arm64", StringComparison.OrdinalIgnoreCase);
    }

    private void ExtractNatives(string versionId, List<LibraryFile> natives)
    {
        if (natives.Count == 0)
            return;

        var nativesDirectory = Path.Combine(_workspace.VersionsDirectory, versionId, "natives");
        Directory.CreateDirectory(nativesDirectory);
        var root = Path.GetFullPath(nativesDirectory);

        foreach (var native in natives)
        {
            var jar = Path.Combine(_workspace.LibrariesDirectory, native.Path);
            if (!File.Exists(jar))
                continue;

            try
            {
                using var archive = ZipFile.OpenRead(jar);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Name) ||
                        entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', '\\')));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                LogService.Warn($"No se pudo extraer la biblioteca nativa '{native.Path}': {ex.Message}");
            }
        }
    }

    /// <summary>Busca un Java compatible; si no existe, descarga el runtime oficial de Mojang.</summary>
    public async Task<JavaRuntimeInfo?> FindJavaAsync(int requiredMajor, string? preferredPath,
        IProgress<JavaInstallProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(new JavaInstallProgress
        {
            Stage = "Buscando Java",
            Detail = $"Se necesita Java {requiredMajor} o superior"
        });

        var detected = await DetectJavaAsync(requiredMajor, preferredPath, cancellationToken);
        if (detected is not null)
            return detected;

        return await InstallRuntimeAsync(requiredMajor, progress, cancellationToken);
    }

    /// <summary>Detecta Java instalado en el sistema sin descargar nada.</summary>
    public async Task<JavaRuntimeInfo?> DetectJavaAsync(int requiredMajor, string? preferredPath,
        CancellationToken cancellationToken = default)
    {
        var best = default(JavaRuntimeInfo);
        foreach (var candidate in EnumerateJavaCandidates(preferredPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = await ProbeJavaAsync(candidate, cancellationToken);
            if (info is null || !info.Is64Bit)
                continue;

            if (info.MajorVersion == requiredMajor)
                return info;

            // Las versiones antiguas (Java 8) no son fiables con runtimes modernos:
            // se prefiere descargar el jre-legacy oficial antes que usar Java 17/21.
            if (requiredMajor <= 8)
                continue;

            if (info.MajorVersion > requiredMajor &&
                (best is null || info.MajorVersion < best.MajorVersion))
                best = info;
        }

        return best;
    }

    private IEnumerable<string> EnumerateJavaCandidates(string? preferredPath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? TryAdd(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            var full = path!.Trim().Trim('"');
            if (!File.Exists(full))
                return null;
            return seen.Add(full) ? full : null;
        }

        static string[] SafeFiles(string directory, int limit)
        {
            try
            {
                return Directory.Exists(directory)
                    ? Directory.EnumerateFiles(directory, "java.exe", SearchOption.AllDirectories)
                        .Take(limit).ToArray()
                    : [];
            }
            catch
            {
                return [];
            }
        }

        static string[] SafeDirectories(string directory, int limit)
        {
            try
            {
                return Directory.Exists(directory)
                    ? Directory.EnumerateDirectories(directory).Take(limit).ToArray()
                    : [];
            }
            catch
            {
                return [];
            }
        }

        if (TryAdd(preferredPath) is { } preferred)
            yield return preferred;

        // Runtime gestionado por el launcher.
        foreach (var exe in SafeFiles(_workspace.RuntimesDirectory, 12))
        {
            if (TryAdd(exe) is { } managed)
                yield return managed;
        }

        foreach (var variable in new[] { "JAVA_HOME", "JDK_HOME" })
        {
            var root = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(root))
                continue;
            if (TryAdd(Path.Combine(root, "bin", "java.exe")) is { } fromVariable)
                yield return fromVariable;
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seen.Count > 40)
                break;
            string candidate;
            try
            {
                candidate = Path.Combine(directory.Trim(), "java.exe");
            }
            catch
            {
                continue;
            }

            if (TryAdd(candidate) is { } fromPath)
                yield return fromPath;
        }

        var roots = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Foundation"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Amazon Corretto"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BellSoft"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zulu"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Semeru"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Eclipse Adoptium")
        };

        foreach (var root in roots)
        {
            foreach (var directory in SafeDirectories(root, 8))
            {
                if (TryAdd(Path.Combine(directory, "bin", "java.exe")) is { } fromRoot)
                    yield return fromRoot;
            }
        }
    }

    private static async Task<JavaRuntimeInfo?> ProbeJavaAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(path, "-version", timeout: TimeSpan.FromSeconds(12),
                cancellationToken: cancellationToken);
            var text = result.Combined;
            var match = JavaVersionRegex.Match(text);
            if (!match.Success)
                return null;

            var versionText = match.Groups[1].Value;
            var major = ParseJavaMajor(versionText);
            if (major <= 0)
                return null;

            var is64 = !text.Contains("32-Bit", StringComparison.OrdinalIgnoreCase);

            return new JavaRuntimeInfo
            {
                Path = path,
                MajorVersion = major,
                Is64Bit = is64,
                VersionText = versionText,
                Source = "Sistema"
            };
        }
        catch
        {
            return null;
        }
    }

    private static int ParseJavaMajor(string version)
    {
        if (version.StartsWith("1.", StringComparison.Ordinal))
        {
            var parts = version.Split('.');
            return parts.Length > 1 && int.TryParse(parts[1], out var legacy) ? legacy : 8;
        }

        var dot = version.IndexOf('.');
        var head = dot > 0 ? version[..dot] : version;
        return int.TryParse(head, out var major) ? major : 0;
    }

    private async Task<JavaRuntimeInfo?> InstallRuntimeAsync(int requiredMajor,
        IProgress<JavaInstallProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report(new JavaInstallProgress
            {
                Stage = "Descargando Java oficial",
                Detail = $"Java {requiredMajor}"
            });

            var json = await DownloadService.TryDownloadTextAsync(RuntimeManifestUrl, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("windows-x64", out var platform) ||
                platform.ValueKind != JsonValueKind.Object)
                return null;

            var preferences = requiredMajor switch
            {
                >= 21 => new[] { "java-runtime-delta", "java-runtime-gamma", "java-runtime-beta", "jre-legacy" },
                >= 17 => new[] { "java-runtime-gamma", "java-runtime-beta", "java-runtime-alpha", "jre-legacy" },
                >= 16 => new[] { "java-runtime-beta", "java-runtime-alpha", "jre-legacy" },
                >= 9 => new[] { "java-runtime-alpha", "jre-legacy" },
                _ => new[] { "jre-legacy", "java-runtime-alpha" }
            };

            string? component = null;
            string? manifestUrl = null;
            foreach (var candidate in preferences)
            {
                if (platform.TryGetProperty(candidate, out var entries) &&
                    entries.ValueKind == JsonValueKind.Array &&
                    entries.GetArrayLength() > 0)
                {
                    component = candidate;
                    manifestUrl = GetString(entries[0].GetProperty("manifest"), "url");
                    break;
                }
            }

            if (component is null || string.IsNullOrWhiteSpace(manifestUrl))
                return null;

            var runtimeDirectory = Path.Combine(_workspace.RuntimesDirectory, component);
            var existing = FindJavaExecutable(runtimeDirectory);
            if (existing is not null)
            {
                var probed = await ProbeJavaAsync(existing, cancellationToken);
                if (probed is not null && probed.MajorVersion >= requiredMajor)
                    return probed;
            }

            var runtimeJson = await DownloadService.TryDownloadTextAsync(manifestUrl!, cancellationToken);
            if (string.IsNullOrWhiteSpace(runtimeJson))
                return null;

            using var runtimeDocument = JsonDocument.Parse(runtimeJson);
            if (!runtimeDocument.RootElement.TryGetProperty("files", out var files) ||
                files.ValueKind != JsonValueKind.Object)
                return null;

            var downloads = new List<(string Name, string Url)>();
            foreach (var file in files.EnumerateObject())
            {
                var type = GetString(file.Value, "type");
                if (type == "directory")
                {
                    Directory.CreateDirectory(Path.Combine(runtimeDirectory,
                        file.Name.Replace('/', Path.DirectorySeparatorChar)));
                    continue;
                }
                if (type != "file")
                    continue;
                if (!file.Value.TryGetProperty("downloads", out var fileDownloads) ||
                    !fileDownloads.TryGetProperty("raw", out var raw))
                    continue;
                var url = GetString(raw, "url");
                if (!string.IsNullOrWhiteSpace(url))
                    downloads.Add((file.Name, url!));
            }

            var done = 0;
            var total = downloads.Count;
            progress?.Report(new JavaInstallProgress
            {
                Stage = "Descargando Java oficial",
                Detail = component,
                Current = 0,
                Total = total
            });

            await Parallel.ForEachAsync(downloads, new ParallelOptions
            {
                MaxDegreeOfParallelism = 6,
                CancellationToken = cancellationToken
            }, async (file, token) =>
            {
                var destination = Path.Combine(runtimeDirectory,
                    file.Name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!File.Exists(destination))
                    await DownloadService.DownloadAsync(file.Url, destination, null, token);
                var current = Interlocked.Increment(ref done);
                if (current % 40 == 0 || current == total)
                {
                    progress?.Report(new JavaInstallProgress
                    {
                        Stage = "Descargando Java oficial",
                        Detail = $"{current} de {total}",
                        Current = current,
                        Total = total
                    });
                }
            });

            var executable = FindJavaExecutable(runtimeDirectory);
            if (executable is null)
                return null;

            var info = await ProbeJavaAsync(executable, cancellationToken);
            if (info is null)
                return null;

            return new JavaRuntimeInfo
            {
                Path = info.Path,
                MajorVersion = info.MajorVersion,
                Is64Bit = info.Is64Bit,
                VersionText = info.VersionText,
                Source = component
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo instalar el runtime oficial de Java.");
            return null;
        }
    }

    private static string? FindJavaExecutable(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
                return null;

            var executables = Directory.EnumerateFiles(directory, "java.exe", SearchOption.AllDirectories).ToList();
            return executables.FirstOrDefault(path =>
                       path.EndsWith(Path.Combine("bin", "java.exe"), StringComparison.OrdinalIgnoreCase))
                   ?? executables.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<JavaLaunchResult> LaunchAsync(string versionId, JavaSession session, int memoryMb,
        string? preferredJavaPath = null, IProgress<JavaInstallProgress>? progress = null,
        CancellationToken cancellationToken = default, string? gameDirectoryOverride = null)
    {
        var versionDirectory = Path.Combine(_workspace.VersionsDirectory, versionId);
        var jsonPath = Path.Combine(versionDirectory, versionId + ".json");
        var jarPath = Path.Combine(versionDirectory, versionId + ".jar");
        if (!File.Exists(jsonPath) || !File.Exists(jarPath))
            return new JavaLaunchResult { Success = false, Message = "La versión no está instalada." };

        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var requiredJava = root.TryGetProperty("javaVersion", out var javaVersion) &&
                           javaVersion.TryGetProperty("majorVersion", out var majorElement)
            ? majorElement.GetInt32()
            : 8;

        var runtime = await FindJavaAsync(requiredJava, preferredJavaPath, progress, cancellationToken);
        if (runtime is null)
        {
            return new JavaLaunchResult
            {
                Success = false,
                Message = $"No se encontró Java {requiredJava} de 64 bits y no se pudo instalar el runtime oficial. " +
                          "Instala Java manualmente o configura su ruta en esta página."
            };
        }

        // Prepare classpath and natives.
        var classpath = new List<string> { jarPath };
        var nativesList = new List<LibraryFile>();
        if (root.TryGetProperty("libraries", out var libraries) &&
            libraries.ValueKind == JsonValueKind.Array)
        {
            foreach (var library in libraries.EnumerateArray())
            {
                if (!IsAllowed(library))
                    continue;

                if (!library.TryGetProperty("downloads", out var downloads) ||
                    downloads.ValueKind != JsonValueKind.Object)
                {
                    var name = GetString(library, "name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var path = Path.Combine(_workspace.LibrariesDirectory, MavenPath(name!));
                        if (IsForeignNative(name, path))
                            continue;

                        if (IsWindowsNative(name, path))
                            nativesList.Add(new LibraryFile(path, string.Empty, null, 0));
                        else if (File.Exists(path))
                            classpath.Add(path);
                    }
                    continue;
                }

                var libraryName = GetString(library, "name") ?? string.Empty;
                if (downloads.TryGetProperty("artifact", out var artifact))
                {
                    var path = GetString(artifact, "path");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var full = Path.Combine(_workspace.LibrariesDirectory, path!);
                        if (IsWindowsNative(libraryName, path))
                            nativesList.Add(new LibraryFile(path!, string.Empty, null, 0));
                        else if (!IsForeignNative(libraryName, path) && File.Exists(full))
                            classpath.Add(full);
                    }
                }

                if (downloads.TryGetProperty("classifiers", out var classifiers) &&
                    library.TryGetProperty("natives", out var nativesElement) &&
                    nativesElement.ValueKind == JsonValueKind.Object)
                {
                    var nativeKey = GetString(nativesElement, "windows");
                    if (!string.IsNullOrWhiteSpace(nativeKey))
                    {
                        var classifier = nativeKey!.Replace("${arch}", "64");
                        if (classifiers.TryGetProperty(classifier, out var nativeArtifact))
                        {
                            var path = GetString(nativeArtifact, "path");
                            var url = GetString(nativeArtifact, "url");
                            if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(url))
                                nativesList.Add(new LibraryFile(path!, url!, null, 0));
                        }
                    }
                }
            }
        }

        ExtractNatives(versionId, nativesList);
        var nativesDirectory = Path.Combine(versionDirectory, "natives");
        Directory.CreateDirectory(nativesDirectory);

        var assetIndexId = "legacy";
        if (root.TryGetProperty("assetIndex", out var assetIndex) &&
            assetIndex.ValueKind == JsonValueKind.Object)
            assetIndexId = GetString(assetIndex, "id") ?? assetIndexId;
        else if (root.TryGetProperty("assets", out var assetsElement))
            assetIndexId = assetsElement.GetString() ?? assetIndexId;

        var loggingFile = FindLoggingFile(versionDirectory);
        var gameDirectory = string.IsNullOrWhiteSpace(gameDirectoryOverride)
            ? _workspace.DefaultGameDirectory
            : gameDirectoryOverride;
        Directory.CreateDirectory(gameDirectory);

        var launchLogPath = Path.Combine(_workspace.LogsDirectory,
            $"java-{versionId}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        Directory.CreateDirectory(_workspace.LogsDirectory);
        var logLock = new object();
        var logHeader = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Minecraft {versionId} · Java {runtime.MajorVersion} · {session.PlayerName} · premium={session.IsPremium}{Environment.NewLine}";
        File.WriteAllText(launchLogPath, logHeader, Encoding.UTF8);

        void AppendGameLog(string line)
        {
            try
            {
                lock (logLock)
                    File.AppendAllText(launchLogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // La salida del juego nunca debe detener el launcher.
            }
        }

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auth_player_name"] = session.PlayerName,
            ["version_name"] = versionId,
            ["game_directory"] = gameDirectory,
            ["assets_root"] = _workspace.AssetsDirectory,
            ["assets_index_name"] = assetIndexId,
            ["auth_uuid"] = session.Uuid,
            ["auth_access_token"] = session.AccessToken,
            ["auth_session"] = session.AccessToken,
            ["user_type"] = session.IsPremium ? "msa" : "legacy",
            ["version_type"] = GetString(root, "type") ?? "release",
            ["natives_directory"] = nativesDirectory,
            ["launcher_name"] = "XDLauncher",
            ["launcher_version"] = AppInfo.Version,
            ["classpath"] = string.Join(';', classpath),
            ["classpath_separator"] = ";",
            ["user_properties"] = "{}",
            ["clientid"] = string.Empty,
            ["auth_xuid"] = string.Empty,
            ["path"] = loggingFile ?? string.Empty
        };

        var memory = Math.Clamp(memoryMb, 1024, 32768);
        var jvmArguments = new List<string>
        {
            $"-Xmx{memory}M",
            $"-Xms{Math.Min(memory, 1024)}M",
            "-Dminecraft.launcher.brand=XDLauncher",
            $"-Dminecraft.launcher.version={AppInfo.Version}"
        };
        var gameArguments = new List<string>();

        if (root.TryGetProperty("arguments", out var arguments) &&
            arguments.ValueKind == JsonValueKind.Object)
        {
            CollectArguments(arguments, "jvm", variables, jvmArguments);
            CollectArguments(arguments, "game", variables, gameArguments);
        }
        else
        {
            jvmArguments.Add($"-Djava.library.path={nativesDirectory}");
            jvmArguments.Add("-cp");
            jvmArguments.Add(variables["classpath"]);

            var legacy = GetString(root, "minecraftArguments") ?? string.Empty;
            foreach (var argument in legacy.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                gameArguments.Add(Substitute(argument, variables));
        }

        var mainClass = GetString(root, "mainClass");
        if (string.IsNullOrWhiteSpace(mainClass))
            return new JavaLaunchResult { Success = false, Message = "La versión no declara una clase principal válida." };

        var startInfo = new ProcessStartInfo(runtime.Path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = gameDirectory
        };

        foreach (var argument in jvmArguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.ArgumentList.Add(mainClass!);
        foreach (var argument in gameArguments)
            startInfo.ArgumentList.Add(argument);

        progress?.Report(new JavaInstallProgress
        {
            Stage = "Iniciando Minecraft",
            Detail = $"{versionId} · Java {runtime.MajorVersion} · {memory} MB"
        });

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
                return;
            lock (output)
                output.AppendLine(args.Data);
            AppendGameLog("[stdout] " + args.Data);
            LogService.Info("[Java] " + args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null)
                return;
            lock (error)
                error.AppendLine(args.Data);
            AppendGameLog("[stderr] " + args.Data);
            LogService.Warn("[Java] " + args.Data);
        };

        try
        {
            if (!process.Start())
                return new JavaLaunchResult { Success = false, Message = "Windows no pudo crear el proceso de Java." };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (_gate)
                _running[versionId] = process;
            process.Exited += (_, _) =>
            {
                lock (_gate)
                    _running.Remove(versionId);
                RunningChanged?.Invoke();
            };
            RunningChanged?.Invoke();

            // Una vez creado el proceso, el lanzamiento ya no debe cancelarse:
            // solo se observa unos segundos para detectar fallos inmediatos.
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);

            if (process.HasExited)
            {
                var code = process.ExitCode;
                var tail = Tail(error.ToString(), 6);
                lock (_gate)
                    _running.Remove(versionId);
                return new JavaLaunchResult
                {
                    Success = code == 0,
                    Message = code == 0
                        ? "Minecraft se cerró inmediatamente después de iniciarse."
                        : $"Minecraft no pudo iniciarse (código {code}). {tail}"
                };
            }

            return new JavaLaunchResult
            {
                Success = true,
                Message = session.IsPremium
                    ? $"Minecraft {versionId} iniciado con la cuenta {session.PlayerName}."
                    : $"Minecraft {versionId} iniciado con el perfil local {session.PlayerName}.",
                ProcessId = process.Id
            };
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo iniciar Minecraft Java.");
            lock (_gate)
                _running.Remove(versionId);
            return new JavaLaunchResult
            {
                Success = false,
                Message = "No se pudo iniciar Minecraft: " + ex.Message
            };
        }
    }

    private static string? FindLoggingFile(string versionDirectory)
    {
        try
        {
            var loggingDirectory = Path.Combine(versionDirectory, "logging");
            if (!Directory.Exists(loggingDirectory))
                return null;
            return Directory.EnumerateFiles(loggingDirectory, "*", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static void CollectArguments(JsonElement arguments, string key,
        Dictionary<string, string> variables, List<string> destination)
    {
        if (!arguments.TryGetProperty(key, out var array) || array.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    destination.Add(Substitute(value, variables));
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
                continue;

            if (!IsAllowed(item))
                continue;

            if (!item.TryGetProperty("value", out var valueElement))
                continue;

            if (valueElement.ValueKind == JsonValueKind.String)
            {
                destination.Add(Substitute(valueElement.GetString() ?? string.Empty, variables));
            }
            else if (valueElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var nested in valueElement.EnumerateArray())
                {
                    if (nested.ValueKind == JsonValueKind.String)
                        destination.Add(Substitute(nested.GetString() ?? string.Empty, variables));
                }
            }
        }
    }

    private static string Substitute(string value, Dictionary<string, string> variables)
    {
        if (!value.Contains("${", StringComparison.Ordinal))
            return value;

        return Regex.Replace(value, "\\$\\{([^}]+)\\}", match =>
            variables.TryGetValue(match.Groups[1].Value, out var replacement) ? replacement : string.Empty);
    }

    private static bool IsAllowed(JsonElement item)
    {
        if (!item.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
            return true;

        var allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            if (!RuleMatches(rule))
                continue;

            var action = GetString(rule, "action");
            allowed = string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase);
        }
        return allowed;
    }

    private static bool RuleMatches(JsonElement rule)
    {
        if (rule.TryGetProperty("os", out var os) && os.ValueKind == JsonValueKind.Object)
        {
            var name = GetString(os, "name");
            if (!string.IsNullOrWhiteSpace(name) &&
                !name!.Equals("windows", StringComparison.OrdinalIgnoreCase))
                return false;

            var arch = GetString(os, "arch");
            if (!string.IsNullOrWhiteSpace(arch) &&
                !arch.Equals("x86_64", StringComparison.OrdinalIgnoreCase) &&
                !arch.Equals("x64", StringComparison.OrdinalIgnoreCase) &&
                !arch.Equals("amd64", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (rule.TryGetProperty("features", out var features) &&
            features.ValueKind == JsonValueKind.Object &&
            features.EnumerateObject().Any())
            return false;

        return true;
    }

    public bool IsRunning(string versionId)
    {
        lock (_gate)
            return _running.TryGetValue(versionId, out var process) && !SafeHasExited(process);
    }

    public bool AnyRunning
    {
        get
        {
            lock (_gate)
                return _running.Values.Any(process => !SafeHasExited(process));
        }
    }

    public void Kill(string versionId)
    {
        Process? process;
        lock (_gate)
            _running.TryGetValue(versionId, out process);
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo cerrar Minecraft: " + ex.Message);
        }
    }

    private static bool SafeHasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private static string Tail(string text, int lines)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var all = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", all.TakeLast(lines).Select(line => line.Trim()));
    }

    /// <summary>UUID offline determinista compatible con el formato de Java (OfflinePlayer:nombre).</summary>
    public static string OfflineUuid(string name)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    private sealed record LibraryFile(string Path, string Url, string? Sha1, long Size);

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long GetLong(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetInt64(out var number)
            ? number
            : 0;
}
