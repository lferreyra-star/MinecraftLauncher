using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace XdLauncher.Services;

public sealed record ModInstallProgress(
    string Stage,
    string Detail,
    int Current,
    int Total,
    double? Percent,
    double? BytesPerSecond,
    TimeSpan? Eta)
{
    public double OverallPercent => Total <= 0 ? 0 : Math.Clamp(Current * 100d / Total, 0, 100);
}

public sealed class InstalledModpack
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string MinecraftVersion { get; set; } = string.Empty;
    public string Loader { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string GameDirectory { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.Now;
}

public sealed class InstalledMod
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string MinecraftVersion { get; set; } = string.Empty;
    public string Loader { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Sha512 { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string Workspace { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string GameDirectory { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.Now;
    public bool IsEnabled { get; set; } = true;
    public List<string> Dependencies { get; set; } = [];
}

/// <summary>
/// Instala mods reales de Modrinth en la carpeta aislada del perfil Java seleccionado.
/// Verifica hashes, resuelve dependencias obligatorias y mantiene una biblioteca persistida.
/// </summary>
public sealed class ModInstallService
{
    private const int MaxDependencyDepth = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly ModrinthService _modrinth;

    public ModInstallService(ModrinthService modrinth)
    {
        _modrinth = modrinth;
    }

    public static string LibraryPath(JavaWorkspace workspace) => Path.Combine(workspace.Root, "mods-library.json");

    public IReadOnlyList<InstalledMod> GetLibrary(JavaWorkspace workspace)
    {
        try
        {
            var path = LibraryPath(workspace);
            if (!File.Exists(path))
                return [];
            return JsonSerializer.Deserialize<List<InstalledMod>>(File.ReadAllText(path), JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo leer la biblioteca de mods: " + ex.Message);
            return [];
        }
    }

    public IReadOnlyList<InstalledMod> GetProfileLibrary(JavaWorkspace workspace, JavaProfile profile)
        => GetLibrary(workspace)
            .Where(mod => string.Equals(mod.ProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public async Task<InstallResult> InstallAsync(
        ModrinthProject project,
        string gameVersion,
        string loader,
        JavaWorkspace workspace,
        JavaProfile profile,
        bool includeOptionalDependencies,
        IProgress<ModInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.Id))
            throw new InvalidOperationException("El proyecto de Modrinth no tiene identificador.");
        if (string.IsNullOrWhiteSpace(gameVersion) || string.IsNullOrWhiteSpace(loader))
            throw new InvalidOperationException("Selecciona una versión de Minecraft y un modloader.");

        workspace.EnsureProfileDirectories(profile);
        var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var optional = new List<string>();
        var installed = new List<InstalledMod>();
        var skipped = new List<string>();

        var versions = await _modrinth.GetVersionsAsync(project.Id, gameVersion, loader, cancellationToken);
        var target = versions.FirstOrDefault(v => ModrinthService.IsCompatible(v, gameVersion, loader));
        if (target is null)
            throw new InvalidOperationException($"No hay una versión de '{project.Name}' compatible con {gameVersion} y {loader}.");

        await InstallVersionRecursiveAsync(target, project, gameVersion, loader, workspace, profile,
            includeOptionalDependencies, 0, processed, optional, installed, skipped, installedNames, progress, cancellationToken);

        SaveLibrary(workspace, installed);
        progress?.Report(new ModInstallProgress("Completado",
            $"{installed.Count} mod(s) instalado(s), {skipped.Count} ya estaban presentes", 1, 1, 100, null, null));

        return new InstallResult(installed, skipped, optional);
    }

    public async Task<IReadOnlyList<InstalledMod>> CheckUpdatesAsync(
        JavaWorkspace workspace,
        JavaProfile profile,
        CancellationToken cancellationToken = default)
    {
        var result = new List<InstalledMod>();
        foreach (var mod in GetProfileLibrary(workspace, profile))
        {
            if (string.IsNullOrWhiteSpace(mod.ProjectId))
                continue;

            var versions = await _modrinth.GetVersionsAsync(mod.ProjectId, mod.MinecraftVersion, mod.Loader, cancellationToken);
            var latest = versions.FirstOrDefault(v => ModrinthService.IsCompatible(v, mod.MinecraftVersion, mod.Loader));
            if (latest is not null && !string.Equals(latest.Id, mod.VersionId, StringComparison.OrdinalIgnoreCase))
                result.Add(mod);
        }
        return result;
    }

    public async Task<InstalledMod> UpdateAsync(
        InstalledMod current,
        JavaWorkspace workspace,
        IProgress<ModInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var target = await _modrinth.GetVersionAsync(current.VersionId, cancellationToken);
        if (target is null)
            target = (await _modrinth.GetVersionsAsync(current.ProjectId, current.MinecraftVersion, current.Loader, cancellationToken))
                .FirstOrDefault(v => ModrinthService.IsCompatible(v, current.MinecraftVersion, current.Loader));
        if (target is null)
            throw new InvalidOperationException("No se encontró una versión de actualización compatible.");

        var primary = SelectPrimary(target, progress);
        if (string.IsNullOrWhiteSpace(primary.Url))
            throw new InvalidOperationException("La versión seleccionada no tiene un archivo descargable.");

        var gameDirectory = string.IsNullOrWhiteSpace(current.GameDirectory)
            ? Path.Combine(workspace.GamesDirectory, Safe(current.MinecraftVersion))
            : current.GameDirectory;
        var modsDirectory = Path.Combine(gameDirectory, "mods");
        Directory.CreateDirectory(modsDirectory);

        var targetPath = Path.Combine(modsDirectory, primary.FileName);
        if (File.Exists(targetPath))
        {
            var backup = targetPath + ".bak-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(targetPath, backup, true);
        }

        await DownloadVerifiedAsync(primary, targetPath, progress, cancellationToken);

        var updated = new InstalledMod
        {
            ProjectId = current.ProjectId,
            Name = current.Name,
            VersionId = target.Id,
            VersionLabel = target.VersionNumber,
            MinecraftVersion = current.MinecraftVersion,
            Loader = current.Loader,
            FileName = primary.FileName,
            Sha512 = primary.Sha512 ?? string.Empty,
            IconUrl = current.IconUrl,
            SourceUrl = current.SourceUrl,
            Workspace = workspace.Kind.ToString(),
            ProfileId = current.ProfileId,
            GameDirectory = current.GameDirectory,
            InstalledAt = DateTimeOffset.Now,
            IsEnabled = current.IsEnabled,
            Dependencies = current.Dependencies
        };

        var library = GetLibrary(workspace).Where(m => !string.Equals(m.ProjectId, current.ProjectId, StringComparison.OrdinalIgnoreCase)).ToList();
        library.Add(updated);
        WriteLibrary(workspace, library);
        return updated;
    }

    public void SetEnabled(InstalledMod mod, JavaWorkspace workspace, bool enabled)
    {
        var directory = FindModDirectory(workspace, mod);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var current = Path.Combine(directory, mod.FileName);
        var disabled = current + ".disabled";
        if (enabled && File.Exists(disabled))
            File.Move(disabled, current, true);
        else if (!enabled && File.Exists(current))
            File.Move(current, disabled, true);

        mod.IsEnabled = enabled;
        UpdateLibraryEntry(workspace, mod);
    }

    public void Remove(InstalledMod mod, JavaWorkspace workspace)
    {
        var directory = FindModDirectory(workspace, mod);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            var path = Path.Combine(directory, mod.FileName);
            var disabled = path + ".disabled";
            if (File.Exists(path))
                File.Delete(path);
            if (File.Exists(disabled))
                File.Delete(disabled);
        }

        var library = GetLibrary(workspace)
            .Where(item => !string.Equals(item.ProjectId, mod.ProjectId, StringComparison.OrdinalIgnoreCase) ||
                           !string.Equals(item.ProfileId, mod.ProfileId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        WriteLibrary(workspace, library);
    }

    public void OpenModsFolder(JavaWorkspace workspace, JavaProfile profile)
    {
        workspace.EnsureProfileDirectories(profile);
        FileOps.OpenFolder(workspace.GetModsDirectory(profile));
    }

    public static string ModpackLibraryPath(JavaWorkspace workspace)
        => Path.Combine(workspace.Root, "modpacks-library.json");

    public IReadOnlyList<InstalledModpack> GetModpackLibrary(JavaWorkspace workspace)
    {
        try
        {
            var path = ModpackLibraryPath(workspace);
            if (!File.Exists(path))
                return [];
            return JsonSerializer.Deserialize<List<InstalledModpack>>(File.ReadAllText(path), JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo leer la biblioteca de modpacks: " + ex.Message);
            return [];
        }
    }

    public void OpenModpackFolder(InstalledModpack pack, JavaWorkspace workspace)
    {
        var directory = string.IsNullOrWhiteSpace(pack.GameDirectory)
            ? Path.Combine(workspace.GamesDirectory, "modpacks", Safe(pack.Name))
            : pack.GameDirectory;
        FileOps.OpenFolder(directory);
    }

    /// <summary>
    /// Instala un modpack .mrpack real: descarga todos sus archivos, aplica overrides,
    /// crea un perfil Java independiente y registra el modpack en su biblioteca.
    /// </summary>
    public async Task<InstalledModpack> InstallModpackAsync(
        ModrinthProject project,
        string fallbackGameVersion,
        JavaWorkspace workspace,
        JavaProfileStore store,
        IProgress<ModInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var versions = await _modrinth.GetVersionsAsync(project.Id, null, null, cancellationToken);
        var version = versions.FirstOrDefault(v => v.Files.Any(f => f.Url.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)))
                      ?? throw new InvalidOperationException("El modpack no tiene un archivo .mrpack descargable.");
        var package = version.Files.First(f => f.Url.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase));

        var tempDirectory = Path.Combine(Path.GetTempPath(), "xdlauncher-modpacks");
        Directory.CreateDirectory(tempDirectory);
        var packagePath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + ".mrpack");
        await DownloadVerifiedAsync(package, packagePath, progress, cancellationToken);

        string minecraftVersion = fallbackGameVersion;
        string loader = JavaModLoaders.Vanilla;
        var filesToDownload = new List<(string RelativePath, ModrinthFile File)>();
        var safeName = Safe($"{project.Slug}-{version.VersionNumber}");
        var gameDirectory = Path.Combine(workspace.GamesDirectory, "modpacks", safeName);
        Directory.CreateDirectory(gameDirectory);

        using (var archive = ZipFile.OpenRead(packagePath))
        {
            var indexEntry = archive.GetEntry("modrinth.index.json")
                ?? throw new InvalidOperationException("El .mrpack no contiene modrinth.index.json.");
            await using var indexStream = indexEntry.Open();
            using var index = await JsonDocument.ParseAsync(indexStream, cancellationToken: cancellationToken);
            var root = index.RootElement;

            if (root.TryGetProperty("dependencies", out var dependencies))
            {
                minecraftVersion = ReadJsonString(dependencies, "minecraft") ?? fallbackGameVersion;
                loader = ReadLoader(dependencies);
            }

            if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in files.EnumerateArray())
                {
                    var path = ReadJsonString(file, "path");
                    var downloads = file.TryGetProperty("downloads", out var urls) && urls.ValueKind == JsonValueKind.Array
                        ? urls.EnumerateArray().Select(u => u.GetString()).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                        : null;
                    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(downloads))
                        continue;

                    var hashes = file.TryGetProperty("hashes", out var h) ? h : default;
                    filesToDownload.Add((path, new ModrinthFile(
                        Path.GetFileName(path),
                        downloads,
                        file.TryGetProperty("size", out var size) && size.TryGetInt64(out var n) ? n : 0,
                        ReadJsonString(hashes, "sha512"),
                        ReadJsonString(hashes, "sha1"),
                        true)));
                }
            }

            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase)))
            {
                var relative = entry.FullName["overrides/".Length..].Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(relative))
                    continue;
                var target = Path.GetFullPath(Path.Combine(gameDirectory, relative));
                if (!target.StartsWith(Path.GetFullPath(gameDirectory), StringComparison.OrdinalIgnoreCase))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, true);
            }
        }

        var profile = new JavaProfile
        {
            VersionId = minecraftVersion,
            ModLoader = loader,
            LoaderVersion = version.VersionNumber,
            PlayerName = store.Config.DefaultPlayerName,
            MemoryMb = Math.Clamp(store.Config.DefaultMemoryMb, 1024, 32768),
            GameDirectory = gameDirectory
        };
        workspace.EnsureProfileDirectories(profile);
        store.SaveProfile(profile);

        for (var i = 0; i < filesToDownload.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = filesToDownload[i];
            var relative = entry.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(Path.Combine(gameDirectory, relative));
            if (!target.StartsWith(Path.GetFullPath(gameDirectory), StringComparison.OrdinalIgnoreCase))
                continue;
            progress?.Report(new ModInstallProgress("Instalando modpack", entry.File.FileName, i + 1,
                filesToDownload.Count, (i + 1) * 100d / Math.Max(filesToDownload.Count, 1), null, null));
            await DownloadVerifiedAsync(entry.File, target, progress, cancellationToken);
        }

        var installed = new InstalledModpack
        {
            ProjectId = project.Id,
            Name = project.Name,
            VersionLabel = version.VersionNumber,
            MinecraftVersion = minecraftVersion,
            Loader = loader,
            ProfileId = profile.Id,
            GameDirectory = gameDirectory,
            Workspace = workspace.Kind.ToString(),
            InstalledAt = DateTimeOffset.Now
        };

        var library = GetModpackLibrary(workspace).ToList();
        library.RemoveAll(item => string.Equals(item.ProjectId, project.Id, StringComparison.OrdinalIgnoreCase));
        library.Add(installed);
        WriteModpackLibrary(workspace, library);

        try { if (File.Exists(packagePath)) File.Delete(packagePath); } catch { }
        progress?.Report(new ModInstallProgress("Completado", project.Name, 1, 1, 100, null, null));
        return installed;
    }

    private static string ReadLoader(JsonElement dependencies)
    {
        if (dependencies.ValueKind != JsonValueKind.Object)
            return JavaModLoaders.Vanilla;

        foreach (var property in dependencies.EnumerateObject())
        {
            var name = property.Name.ToLowerInvariant();
            if (name is "fabric-loader" or "fabric")
                return JavaModLoaders.Fabric;
            if (name is "forge")
                return JavaModLoaders.Forge;
            if (name is "neoforge")
                return JavaModLoaders.NeoForge;
            if (name is "quilt-loader" or "quilt")
                return JavaModLoaders.Quilt;
        }
        return JavaModLoaders.Vanilla;
    }

    private static string? ReadJsonString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void WriteModpackLibrary(JavaWorkspace workspace, IReadOnlyList<InstalledModpack> library)
    {
        try
        {
            Directory.CreateDirectory(workspace.Root);
            var path = ModpackLibraryPath(workspace);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(library, JsonOptions));
            File.Move(temp, path, true);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo guardar la biblioteca de modpacks.");
        }
    }

    private async Task InstallVersionRecursiveAsync(
        ModrinthVersion version,
        ModrinthProject project,
        string gameVersion,
        string loader,
        JavaWorkspace workspace,
        JavaProfile profile,
        bool includeOptionalDependencies,
        int depth,
        HashSet<string> processed,
        List<string> optional,
        List<InstalledMod> installed,
        List<string> skipped,
        HashSet<string> installedNames,
        IProgress<ModInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (depth > MaxDependencyDepth)
            throw new InvalidOperationException("Se superó el límite de profundidad de dependencias.");
        if (!processed.Add(version.Id))
            return;

        progress?.Report(new ModInstallProgress("Resolviendo dependencias",
            $"{project.Name} · {version.VersionNumber}", depth + 1, MaxDependencyDepth, null, null, null));

        foreach (var dependency in version.Dependencies)
        {
            if (string.Equals(dependency.Type, "optional", StringComparison.OrdinalIgnoreCase))
            {
                optional.Add(dependency.ProjectId ?? dependency.VersionId ?? "desconocida");
                if (!includeOptionalDependencies)
                    continue;
            }
            else if (!string.Equals(dependency.Type, "required", StringComparison.OrdinalIgnoreCase))
                continue;

            var dependencyProjectId = dependency.ProjectId;

            if (!string.IsNullOrWhiteSpace(dependency.VersionId))
            {
                var dependencyVersion = await _modrinth.GetVersionAsync(dependency.VersionId, cancellationToken);
                dependencyProjectId ??= dependencyVersion?.ProjectId;
                if (dependencyVersion is not null &&
                    ModrinthService.IsCompatible(dependencyVersion, gameVersion, loader) &&
                    !string.IsNullOrWhiteSpace(dependencyProjectId))
                {
                    var dependencyProject = await _modrinth.GetProjectAsync(dependencyProjectId, cancellationToken);
                    if (dependencyProject is not null)
                        await InstallVersionRecursiveAsync(dependencyVersion, dependencyProject, gameVersion, loader,
                            workspace, profile, includeOptionalDependencies, depth + 1, processed, optional,
                            installed, skipped, installedNames, progress, cancellationToken);
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(dependencyProjectId))
            {
                var dependencyProject = await _modrinth.GetProjectAsync(dependencyProjectId, cancellationToken);
                if (dependencyProject is null)
                    continue;
                var dependencyVersions = await _modrinth.GetVersionsAsync(dependencyProject.Id, gameVersion, loader, cancellationToken);
                var dependencyVersion = dependencyVersions.FirstOrDefault(v => ModrinthService.IsCompatible(v, gameVersion, loader));
                if (dependencyVersion is not null)
                    await InstallVersionRecursiveAsync(dependencyVersion, dependencyProject, gameVersion, loader,
                        workspace, profile, includeOptionalDependencies, depth + 1, processed, optional,
                        installed, skipped, installedNames, progress, cancellationToken);
            }
        }

        var file = SelectPrimary(version, progress);
        if (string.IsNullOrWhiteSpace(file.Url))
            throw new InvalidOperationException($"'{project.Name}' no tiene un archivo principal descargable.");

        var modsDirectory = workspace.GetModsDirectory(profile);
        Directory.CreateDirectory(modsDirectory);
        var targetPath = Path.Combine(modsDirectory, file.FileName);

        if (File.Exists(targetPath) && await HashMatchesAsync(targetPath, file, cancellationToken))
        {
            skipped.Add(file.FileName);
            installedNames.Add(file.FileName);
            return;
        }

        await DownloadVerifiedAsync(file, targetPath, progress, cancellationToken);
        installedNames.Add(file.FileName);

        installed.Add(new InstalledMod
        {
            ProjectId = project.Id,
            Name = project.Name,
            VersionId = version.Id,
            VersionLabel = version.VersionNumber,
            MinecraftVersion = gameVersion,
            Loader = loader,
            FileName = file.FileName,
            Sha512 = file.Sha512 ?? string.Empty,
            IconUrl = project.IconUrl,
            SourceUrl = project.SourceUrl,
            Workspace = workspace.Kind.ToString(),
            ProfileId = profile.Id,
            GameDirectory = workspace.GetGameDirectory(profile),
            Dependencies = version.Dependencies
                .Where(d => string.Equals(d.Type, "required", StringComparison.OrdinalIgnoreCase))
                .Select(d => d.ProjectId ?? d.VersionId ?? string.Empty)
                .Where(s => s.Length > 0)
                .ToList()
        });
    }

    private static ModrinthFile SelectPrimary(ModrinthVersion version, IProgress<ModInstallProgress>? progress)
    {
        var file = version.Files.FirstOrDefault(f => f.Primary && f.Url.Length > 0)
                   ?? version.Files.FirstOrDefault(f => f.Url.Length > 0);
        if (file is null)
            throw new InvalidOperationException($"'{version.Name}' no tiene archivos descargables.");
        return file;
    }

    private static async Task DownloadVerifiedAsync(
        ModrinthFile file,
        string targetPath,
        IProgress<ModInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "xdlauncher-mods");
        Directory.CreateDirectory(tempDirectory);
        var tempPath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + ".jar");

        try
        {
            var speed = new SpeedTracker();
            await DownloadService.DownloadAsync(file.Url, tempPath,
                new Progress<DownloadProgress>(p =>
                {
                    var rate = speed.Rate(p.BytesReceived);
                    progress?.Report(new ModInstallProgress("Descargando", file.FileName, 0, 1,
                        p.Percent, rate, speed.Eta(p.BytesReceived, p.TotalBytes)));
                }), cancellationToken);

            if (!await HashMatchesAsync(tempPath, file, cancellationToken))
            {
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
                throw new InvalidOperationException($"La verificación de integridad falló para '{file.FileName}'.");
            }

            var targetDirectory = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(targetDirectory);
            File.Move(tempPath, targetPath, true);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private static async Task<bool> HashMatchesAsync(string path, ModrinthFile file, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(file.Sha512))
        {
            await using var stream = File.OpenRead(path);
            var hash = await SHA512.HashDataAsync(stream, cancellationToken);
            return string.Equals(Convert.ToHexString(hash), file.Sha512, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(file.Sha1))
        {
            await using var stream = File.OpenRead(path);
            var hash = await SHA1.HashDataAsync(stream, cancellationToken);
            return string.Equals(Convert.ToHexString(hash), file.Sha1, StringComparison.OrdinalIgnoreCase);
        }

        return new FileInfo(path).Length == file.Size && file.Size > 0;
    }

    private string? FindModDirectory(JavaWorkspace workspace, InstalledMod mod)
    {
        if (!Enum.TryParse<JavaWorkspaceKind>(mod.Workspace, ignoreCase: true, out var kind) || kind != workspace.Kind)
        {
            var other = kind == JavaWorkspaceKind.Premium
                ? Paths.CreatePremiumJavaWorkspace()
                : Paths.CreateNoPremiumJavaWorkspace();
            workspace = other;
        }

        var stored = GetLibrary(workspace).FirstOrDefault(m =>
            string.Equals(m.ProjectId, mod.ProjectId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.ProfileId, mod.ProfileId, StringComparison.OrdinalIgnoreCase));
        if (stored is null)
            return null;

        var gameDirectory = string.IsNullOrWhiteSpace(stored.GameDirectory)
            ? Path.Combine(workspace.GamesDirectory, Safe(stored.MinecraftVersion))
            : stored.GameDirectory;
        return Path.Combine(gameDirectory, "mods");
    }

    private static string Safe(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "version" : clean;
    }

    private IReadOnlyList<InstalledMod> SaveLibrary(JavaWorkspace workspace, IReadOnlyList<InstalledMod> newlyInstalled)
    {
        var library = GetLibrary(workspace).ToList();
        foreach (var mod in newlyInstalled)
        {
            library.RemoveAll(item =>
                string.Equals(item.ProjectId, mod.ProjectId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.ProfileId, mod.ProfileId, StringComparison.OrdinalIgnoreCase));
            library.Add(mod);
        }
        WriteLibrary(workspace, library);
        return library;
    }

    private void UpdateLibraryEntry(JavaWorkspace workspace, InstalledMod mod)
    {
        var library = GetLibrary(workspace).ToList();
        var index = library.FindIndex(item =>
            string.Equals(item.ProjectId, mod.ProjectId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.ProfileId, mod.ProfileId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            library[index] = mod;
        WriteLibrary(workspace, library);
    }

    private static void WriteLibrary(JavaWorkspace workspace, IReadOnlyList<InstalledMod> library)
    {
        try
        {
            var path = workspace.Root;
            Directory.CreateDirectory(path);
            var temp = LibraryPath(workspace) + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(library, JsonOptions));
            File.Move(temp, LibraryPath(workspace), true);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo guardar la biblioteca de mods.");
        }
    }

    private sealed class SpeedTracker
    {
        private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
        private long _lastBytes;
        private double _rate;

        public double Rate(long bytes)
        {
            var seconds = _watch.Elapsed.TotalSeconds;
            if (seconds <= 0.2)
                return 0;

            var current = (bytes - _lastBytes) / seconds;
            _rate = _rate <= 0 ? current : _rate * 0.65 + current * 0.35;
            _lastBytes = bytes;
            _watch.Restart();
            return _rate;
        }

        public TimeSpan? Eta(long bytes, long? total)
        {
            if (total is null or <= 0 || _rate <= 0 || bytes >= total.Value)
                return null;
            return TimeSpan.FromSeconds((total.Value - bytes) / _rate);
        }
    }
}

public sealed record InstallResult(
    IReadOnlyList<InstalledMod> Installed,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> OptionalDependencies);
