namespace XdLauncher.Services;

/// <summary>All launcher and Minecraft user-data paths used by the application.</summary>
public static class Paths
{
    public const string RetailPackageFamily = "Microsoft.MinecraftUWP_8wekyb3d8bbwe";
    public const string PreviewPackageFamily = "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe";
    public const string RetailPackageName = "Microsoft.MinecraftUWP";
    public const string PreviewPackageName = "Microsoft.MinecraftWindowsBeta";
    public const string RetailAumid = RetailPackageFamily + "!App";
    public const string PreviewAumid = PreviewPackageFamily + "!App";

    public static string AppDataRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XDLauncher");

    /// <summary>
    /// The installer can point the launcher to a data folder in any drive (for example D:\MinecraftData).
    /// It writes the folder in data.path.txt next to the executable and this method applies it.
    /// </summary>
    public static void ApplyConfiguredDataRoot()
    {
        try
        {
            var pointer = Path.Combine(AppContext.BaseDirectory, "data.path.txt");
            if (!File.Exists(pointer))
                return;

            var configured = File.ReadAllText(pointer).Trim();
            if (string.IsNullOrWhiteSpace(configured))
                return;

            var full = Path.GetFullPath(configured);
            Directory.CreateDirectory(full);
            AppDataRoot = full;
            LogService.Info("Carpeta de datos personalizada: " + full);
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo aplicar la carpeta de datos personalizada: " + ex.Message);
        }
    }

    public static string SettingsFile => Path.Combine(AppDataRoot, "settings.json");
    public static string AccountFile => Path.Combine(AppDataRoot, "account.json");
    public static string LogsDirectory => Path.Combine(AppDataRoot, "logs");
    public static string CacheDirectory => Path.Combine(AppDataRoot, "cache");
    public static string ImagesCacheDirectory => Path.Combine(CacheDirectory, "images");
    public static string LibraryDirectory => Path.Combine(AppDataRoot, "library");
    public static string BackgroundsDirectory => Path.Combine(AppDataRoot, "backgrounds");
    public static string AvatarsDirectory => Path.Combine(AppDataRoot, "avatars");
    public static string AddonCacheDirectory => Path.Combine(CacheDirectory, "addons");

    // Mobile Connect tools live inside the launcher data folder so they survive updates.
    public static string ToolsDirectory => Path.Combine(AppDataRoot, "tools");
    public static string AndroidToolsDirectory => Path.Combine(ToolsDirectory, "android");
    public static string PlatformToolsDirectory => Path.Combine(AndroidToolsDirectory, "platform-tools");
    public static string ScrcpyDirectory => Path.Combine(ToolsDirectory, "scrcpy");
    public static string DownloadsDirectory => Path.Combine(CacheDirectory, "downloads");
    public static string MobileCapturesDirectory => Path.Combine(CacheDirectory, "mobile");

    // Minecraft Java Edition legacy data (kept for backward compatibility only).
    public static string JavaDirectory => Path.Combine(AppDataRoot, "java");
    public static string JavaVersionsDirectory => Path.Combine(JavaDirectory, "versions");
    public static string JavaLibrariesDirectory => Path.Combine(JavaDirectory, "libraries");
    public static string JavaAssetsDirectory => Path.Combine(JavaDirectory, "assets");
    public static string JavaGameDirectory => Path.Combine(JavaDirectory, "game");
    public static string JavaRuntimesDirectory => Path.Combine(JavaDirectory, "runtimes");

    // Independent Java Edition workspaces: Premium and No-Premium never share files.
    public static string MinecraftLauncherDataRoot => Path.Combine(AppDataRoot, "MinecraftLauncher");
    public static string PremiumJavaRoot => Path.Combine(MinecraftLauncherDataRoot, "Premium");
    public static string NoPremiumJavaRoot => Path.Combine(MinecraftLauncherDataRoot, "NoPremium");

    public static JavaWorkspace CreatePremiumJavaWorkspace()
        => new(JavaWorkspaceKind.Premium, PremiumJavaRoot);

    public static JavaWorkspace CreateNoPremiumJavaWorkspace()
        => new(JavaWorkspaceKind.NoPremium, NoPremiumJavaRoot);

    public static string LocalAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string? GetComMojangPath(string packageFamily)
    {
        var path = Path.Combine(LocalAppData, "Packages", packageFamily, "LocalState", "games", "com.mojang");
        return Directory.Exists(path) ? path : null;
    }

    public static string GetComMojangPathOrExpected(string packageFamily)
        => Path.Combine(LocalAppData, "Packages", packageFamily, "LocalState", "games", "com.mojang");

    public static IEnumerable<string> ExistingComMojangPaths()
    {
        foreach (var family in new[] { RetailPackageFamily, PreviewPackageFamily })
        {
            var path = GetComMojangPath(family);
            if (path is not null)
                yield return path;
        }
    }

    public static void EnsureLauncherDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(ImagesCacheDirectory);
        Directory.CreateDirectory(LibraryDirectory);
        Directory.CreateDirectory(BackgroundsDirectory);
        Directory.CreateDirectory(AvatarsDirectory);
        Directory.CreateDirectory(AddonCacheDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ToolsDirectory);
        Directory.CreateDirectory(AndroidToolsDirectory);
        Directory.CreateDirectory(DownloadsDirectory);
        Directory.CreateDirectory(MobileCapturesDirectory);
        Directory.CreateDirectory(JavaDirectory);
        Directory.CreateDirectory(JavaVersionsDirectory);
        Directory.CreateDirectory(JavaLibrariesDirectory);
        Directory.CreateDirectory(JavaAssetsDirectory);
        Directory.CreateDirectory(JavaGameDirectory);
        Directory.CreateDirectory(JavaRuntimesDirectory);

        CreatePremiumJavaWorkspace().EnsureDirectories();
        CreateNoPremiumJavaWorkspace().EnsureDirectories();
    }

    /// <summary>Official Android SDK locations, including the adb bundled by the launcher.</summary>
    public static IEnumerable<string> CandidateSdkRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[]
                 {
                     Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                     Environment.GetEnvironmentVariable("ANDROID_HOME"),
                     Path.Combine(LocalAppData, "Android", "Sdk"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Android", "Sdk"),
                     @"C:\Android\Sdk"
                 })
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;
            var full = root.TrimEnd('\\', '/');
            if (seen.Add(full) && Directory.Exists(full))
                yield return full;
        }
    }

    /// <summary>
    /// Built-in backgrounds live inside the assembly as WPF resources, so they are
    /// addressed with a pack URI instead of a file on disk.
    /// </summary>
    public static string? GetBuiltInBackground(string id)
    {
        var name = id switch
        {
            "bosque" => "bg-bosque.png",
            "aurora" => "bg-aurora.png",
            "amanecer" => "bg-amanecer.png",
            "cueva" => "bg-cueva.png",
            _ => null
        };
        if (name is null)
            return null;

        return "pack://application:,,,/Assets/backgrounds/" + name;
    }

    public static string? GetBackgroundPath(string id, string? customPath)
    {
        if (id == "custom" && !string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return customPath;
        return GetBuiltInBackground(id) ?? GetBuiltInBackground("bosque");
    }

    public static string DefaultAvatarPath => "pack://application:,,,/Assets/avatar-default.png";

    public static string GetAvatarCachePath(string key)
    {
        var safe = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(ImagesCacheDirectory, $"avatar-{safe[..16]}.png");
    }
}
