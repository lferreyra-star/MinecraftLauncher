using System.Text.Json;
using System.Text.Json.Serialization;
using XdLauncher.Core;

namespace XdLauncher.Services;

public sealed class TouchPointSetting
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>Persisted configuration for the phone-to-PC streaming feature.</summary>
public sealed class MobileConnectSettings
{
    public string LastSerial { get; set; } = string.Empty;
    public string LastWifiAddress { get; set; } = string.Empty;
    public bool AutoReconnect { get; set; } = true;
    public string Quality { get; set; } = "HD";
    public int MaxFps { get; set; } = 60;
    public bool AudioEnabled { get; set; } = true;
    public bool StayAwake { get; set; } = true;
    public bool ShowTouches { get; set; }
    public bool TouchMappingEnabled { get; set; }
    public bool KeyboardMouseEnabled { get; set; } = true;
    public double JoystickRadius { get; set; } = 0.11;
    public Dictionary<string, TouchPointSetting> TouchTargets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> KeyBindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Persisted configuration for Minecraft Java Edition.</summary>
public sealed class JavaSettings
{
    public string PlayerName { get; set; } = "Jugador";
    public int MemoryMb { get; set; } = 4096;
    public string JavaPath { get; set; } = string.Empty;
    public string LastVersionId { get; set; } = string.Empty;
    public string GameDirectory { get; set; } = string.Empty;
}

public sealed class LauncherSettings
{
    public int SchemaVersion { get; set; } = 3;
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "Verde";
    public string BackgroundId { get; set; } = "bosque";
    public string? CustomBackgroundPath { get; set; }
    public double UiScale { get; set; } = 1.0;
    public int WindowWidth { get; set; } = 1440;
    public int WindowHeight { get; set; } = 900;
    public bool WindowMaximized { get; set; }
    public bool MinimizeOnLaunch { get; set; } = true;
    public bool CloseOnLaunch { get; set; }
    public bool VerifyBeforeLaunch { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ShowNews { get; set; } = true;
    public string LocalProfileName { get; set; } = "Jugador";
    public string? LocalAvatarPath { get; set; }
    public bool UseLocalProfile { get; set; } = true;
    public string MicrosoftClientId { get; set; } = string.Empty;
    public bool AutoRestoreAccount { get; set; } = true;
    public string PreferredChannel { get; set; } = "Retail";
    public DateTimeOffset? LastUpdateCheck { get; set; }
    public bool AskPlatformOnStartup { get; set; } = true;
    public MobileConnectSettings MobileConnect { get; set; } = new();
    public JavaSettings Java { get; set; } = new();

    [JsonIgnore]
    public string ThemeLabel => Theme == "Light" ? "Claro" : "Oscuro";
}

public sealed class SettingsService
{
    private readonly string _settingsFile;
    private readonly object _gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public SettingsService(string? settingsFile = null)
    {
        _settingsFile = settingsFile ?? Paths.SettingsFile;
        Settings = Load();
    }

    public LauncherSettings Settings { get; private set; }

    public event Action<LauncherSettings>? Saved;

    public LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFile))
                return new LauncherSettings();

            var json = File.ReadAllText(_settingsFile);
            var loaded = JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions);
            if (loaded is null)
                return new LauncherSettings();

            loaded.WindowWidth = Math.Clamp(loaded.WindowWidth, 1100, 3840);
            loaded.WindowHeight = Math.Clamp(loaded.WindowHeight, 700, 2160);
            loaded.UiScale = Math.Clamp(loaded.UiScale, 0.85, 1.15);
            if (loaded.BackgroundId is not ("bosque" or "aurora" or "amanecer" or "cueva" or "custom"))
                loaded.BackgroundId = "bosque";
            if (loaded.Theme is not ("Dark" or "Light"))
                loaded.Theme = "Dark";
            if (loaded.PreferredChannel is not ("Retail" or "Preview"))
                loaded.PreferredChannel = "Retail";

            loaded.MobileConnect ??= new MobileConnectSettings();
            var mobile = loaded.MobileConnect;
            mobile.Quality = mobile.Quality is ("Native" or "HD" or "FHD") ? mobile.Quality : "HD";
            mobile.MaxFps = Math.Clamp(mobile.MaxFps, 15, 240);
            mobile.TouchTargets ??= new(StringComparer.OrdinalIgnoreCase);
            mobile.KeyBindings ??= new(StringComparer.OrdinalIgnoreCase);

            loaded.Java ??= new JavaSettings();
            loaded.Java.MemoryMb = Math.Clamp(loaded.Java.MemoryMb, 1024, 32768);
            if (string.IsNullOrWhiteSpace(loaded.Java.PlayerName))
                loaded.Java.PlayerName = "Jugador";
            return loaded;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo leer settings.json; se usarán valores predeterminados.");
            return new LauncherSettings();
        }
    }

    public void Save(LauncherSettings? settings = null)
    {
        if (settings is not null)
            Settings = settings;

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
                var temp = _settingsFile + ".tmp";
                var json = JsonSerializer.Serialize(Settings, JsonOptions);
                File.WriteAllText(temp, json);
                File.Move(temp, _settingsFile, true);
            }
            Saved?.Invoke(Settings);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo guardar settings.json.");
        }
    }

    public void Reset()
    {
        Settings = new LauncherSettings();
        Save();
    }
}
