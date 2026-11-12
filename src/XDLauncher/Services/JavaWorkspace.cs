using System.Text.Json;
using XdLauncher.Models;

namespace XdLauncher.Services;

public enum JavaWorkspaceKind
{
    Premium,
    NoPremium
}

/// <summary>
/// Independent Java Edition installation. Every workspace owns its versions, libraries,
/// assets, runtimes, game folders, mods, logs and profile configuration.
/// </summary>
public sealed class JavaWorkspace
{
    public JavaWorkspace(JavaWorkspaceKind kind, string root)
    {
        Kind = kind;
        Root = Path.GetFullPath(root);
    }

    public JavaWorkspaceKind Kind { get; }
    public string Root { get; }
    public string DisplayName => Kind == JavaWorkspaceKind.Premium ? "Premium" : "No-Premium";
    public string FolderName => Kind == JavaWorkspaceKind.Premium ? "Premium" : "NoPremium";
    public bool IsPremium => Kind == JavaWorkspaceKind.Premium;

    public string VersionsDirectory => Path.Combine(Root, "versions");
    public string LibrariesDirectory => Path.Combine(Root, "libraries");
    public string AssetsDirectory => Path.Combine(Root, "assets");
    public string RuntimesDirectory => Path.Combine(Root, "runtimes");
    public string GamesDirectory => Path.Combine(Root, "games");
    public string LogsDirectory => Path.Combine(Root, "logs");
    public string ProfilesFile => Path.Combine(Root, "profiles.json");
    public string ConfigFile => Path.Combine(Root, "workspace.json");
    public string CacheManifestFile => Path.Combine(Root, "java-manifest.json");
    public string DefaultGameDirectory => Path.Combine(Root, "game");

    public string GetGameDirectory(JavaProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.GameDirectory))
            return Path.GetFullPath(profile.GameDirectory);

        return Path.Combine(GamesDirectory, SafeDirectoryName(profile.VersionId));
    }

    public string GetModsDirectory(JavaProfile profile)
        => Path.Combine(GetGameDirectory(profile), "mods");

    public string GetSavesDirectory(JavaProfile profile)
        => Path.Combine(GetGameDirectory(profile), "saves");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(VersionsDirectory);
        Directory.CreateDirectory(LibrariesDirectory);
        Directory.CreateDirectory(AssetsDirectory);
        Directory.CreateDirectory(RuntimesDirectory);
        Directory.CreateDirectory(GamesDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DefaultGameDirectory);
    }

    private static string SafeDirectoryName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "version" : clean;
    }
}

/// <summary>Per-version local configuration. Never contains Microsoft tokens.</summary>
public sealed class JavaProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string VersionId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = "Jugador";
    public int MemoryMb { get; set; } = 4096;
    public string JavaPath { get; set; } = string.Empty;
    public string GameDirectory { get; set; } = string.Empty;
    public string ModLoader { get; set; } = JavaModLoaders.Vanilla;
    public string FabricLoaderVersion { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastPlayedAt { get; set; }
}

public static class JavaModLoaders
{
    public const string Vanilla = "Vanilla";
    public const string Fabric = "Fabric";
}

public sealed class JavaWorkspaceConfig
{
    public string DefaultPlayerName { get; set; } = "Jugador";
    public int DefaultMemoryMb { get; set; } = 4096;
    public string DefaultJavaPath { get; set; } = string.Empty;
    public string LastVersionId { get; set; } = string.Empty;
}

/// <summary>Thread-safe profiles/workspace configuration stored inside one workspace only.</summary>
public sealed class JavaProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly object _gate = new();
    private readonly JavaWorkspace _workspace;
    private List<JavaProfile> _profiles;

    public JavaProfileStore(JavaWorkspace workspace)
    {
        _workspace = workspace;
        _workspace.EnsureDirectories();
        WasInitialized = File.Exists(_workspace.ConfigFile);
        Config = LoadConfig();
        _profiles = LoadProfiles();
    }

    public JavaWorkspace Workspace => _workspace;
    public JavaWorkspaceConfig Config { get; private set; }
    public bool WasInitialized { get; }

    public IReadOnlyList<JavaProfile> Profiles
    {
        get
        {
            lock (_gate)
                return _profiles.ToList();
        }
    }

    public JavaProfile? Find(string versionId)
    {
        lock (_gate)
        {
            return _profiles.FirstOrDefault(profile =>
                profile.VersionId.Equals(versionId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public JavaProfile GetOrCreate(string versionId)
    {
        lock (_gate)
        {
            var existing = _profiles.FirstOrDefault(profile =>
                profile.VersionId.Equals(versionId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            var profile = new JavaProfile
            {
                VersionId = versionId,
                PlayerName = string.IsNullOrWhiteSpace(Config.DefaultPlayerName)
                    ? "Jugador"
                    : Config.DefaultPlayerName,
                MemoryMb = Math.Clamp(Config.DefaultMemoryMb, 1024, 32768),
                JavaPath = Config.DefaultJavaPath
            };
            _profiles.Add(profile);
            SaveProfilesLocked();
            return profile;
        }
    }

    public void SaveProfile(JavaProfile profile)
    {
        lock (_gate)
        {
            var index = _profiles.FindIndex(item =>
                item.VersionId.Equals(profile.VersionId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                _profiles[index] = profile;
            else
                _profiles.Add(profile);
            SaveProfilesLocked();
        }
    }

    public void SaveConfig()
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_workspace.Root);
                var temp = _workspace.ConfigFile + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(Config, JsonOptions));
                File.Move(temp, _workspace.ConfigFile, true);
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "No se pudo guardar la configuración del workspace Java.");
            }
        }
    }

    private List<JavaProfile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(_workspace.ProfilesFile))
                return [];

            var loaded = JsonSerializer.Deserialize<List<JavaProfile>>(
                File.ReadAllText(_workspace.ProfilesFile), JsonOptions);
            if (loaded is null)
                return [];

            foreach (var profile in loaded)
            {
                profile.MemoryMb = Math.Clamp(profile.MemoryMb, 1024, 32768);
                profile.PlayerName = NormalizePlayerName(profile.PlayerName);
                profile.ModLoader = profile.ModLoader == JavaModLoaders.Fabric
                    ? JavaModLoaders.Fabric
                    : JavaModLoaders.Vanilla;
            }

            return loaded
                .Where(profile => !string.IsNullOrWhiteSpace(profile.VersionId))
                .GroupBy(profile => profile.VersionId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudieron leer los perfiles Java del workspace.");
            return [];
        }
    }

    private JavaWorkspaceConfig LoadConfig()
    {
        try
        {
            if (!File.Exists(_workspace.ConfigFile))
                return new JavaWorkspaceConfig();

            var loaded = JsonSerializer.Deserialize<JavaWorkspaceConfig>(
                File.ReadAllText(_workspace.ConfigFile), JsonOptions);
            if (loaded is null)
                return new JavaWorkspaceConfig();

            loaded.DefaultMemoryMb = Math.Clamp(loaded.DefaultMemoryMb, 1024, 32768);
            loaded.DefaultPlayerName = NormalizePlayerName(loaded.DefaultPlayerName);
            return loaded;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo leer la configuración del workspace Java.");
            return new JavaWorkspaceConfig();
        }
    }

    private void SaveProfilesLocked()
    {
        try
        {
            Directory.CreateDirectory(_workspace.Root);
            var temp = _workspace.ProfilesFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_profiles, JsonOptions));
            File.Move(temp, _workspace.ProfilesFile, true);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudieron guardar los perfiles Java del workspace.");
        }
    }

    private static string NormalizePlayerName(string? value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "Jugador" : value.Trim();
        return name.Length > 16 ? name[..16] : name;
    }
}
