using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using XdLauncher.Core;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

public sealed class BackgroundOption
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string PreviewPath => Path;
}

public sealed class AccentOption
{
    public required string Name { get; init; }
    public required string Hex { get; init; }
}

public sealed class WindowSizeOption
{
    public required string Label { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>Live settings state with immediate persistence and visual application.</summary>
public sealed class SettingsState : ObservableObject
{
    private readonly SettingsService _service;
    private readonly DialogService _dialogs;
    private readonly SystemInfoService _systemInfo;
    private bool _isApplying;

    public SettingsState(SettingsService service, DialogService dialogs, SystemInfoService systemInfo)
    {
        _service = service;
        _dialogs = dialogs;
        _systemInfo = systemInfo;

        Backgrounds = new ObservableCollection<BackgroundOption>
        {
            new() { Id = "bosque", Name = "Bosque cinematográfico", Path = Paths.GetBuiltInBackground("bosque") ?? string.Empty },
            new() { Id = "aurora", Name = "Aurora boreal", Path = Paths.GetBuiltInBackground("aurora") ?? string.Empty },
            new() { Id = "amanecer", Name = "Amanecer en las montañas", Path = Paths.GetBuiltInBackground("amanecer") ?? string.Empty },
            new() { Id = "cueva", Name = "Cueva de cristal", Path = Paths.GetBuiltInBackground("cueva") ?? string.Empty },
            new() { Id = "nether", Name = "Nether cinematográfico", Path = Paths.GetBuiltInBackground("nether") ?? string.Empty },
            new() { Id = "end", Name = "El End", Path = Paths.GetBuiltInBackground("end") ?? string.Empty },
            new() { Id = "cerezo", Name = "Bioma de cerezo", Path = Paths.GetBuiltInBackground("cerezo") ?? string.Empty },
            new() { Id = "nevado", Name = "Montañas nevadas", Path = Paths.GetBuiltInBackground("nevado") ?? string.Empty },
            new() { Id = "ciudad", Name = "Ciudad nocturna", Path = Paths.GetBuiltInBackground("ciudad") ?? string.Empty },
            new() { Id = "nocturno", Name = "Bosque nocturno", Path = Paths.GetBuiltInBackground("nocturno") ?? string.Empty },
            new() { Id = "magico", Name = "Bosque mágico", Path = Paths.GetBuiltInBackground("magico") ?? string.Empty },
            new() { Id = "fantastico", Name = "Mundos fantásticos", Path = Paths.GetBuiltInBackground("fantastico") ?? string.Empty }
        };
        AddCustomBackgroundIfPresent();

        Accents =
        [
            new AccentOption { Name = "Verde", Hex = "#5CE07A" },
            new AccentOption { Name = "Esmeralda", Hex = "#2DD4A7" },
            new AccentOption { Name = "Lima", Hex = "#B4E64B" },
            new AccentOption { Name = "Cian", Hex = "#45C8F5" },
            new AccentOption { Name = "Océano", Hex = "#4F8DFD" },
            new AccentOption { Name = "Violeta", Hex = "#A78BFA" },
            new AccentOption { Name = "Rosa", Hex = "#F472B6" },
            new AccentOption { Name = "Ámbar", Hex = "#F5B942" },
            new AccentOption { Name = "Coral", Hex = "#F87171" },
            new AccentOption { Name = "XD Emerald", Hex = "#5CE07A" },
            new AccentOption { Name = "Obsidian", Hex = "#64748B" },
            new AccentOption { Name = "Cyberpunk", Hex = "#F472B6" },
            new AccentOption { Name = "Aurora", Hex = "#34D399" },
            new AccentOption { Name = "Midnight", Hex = "#6366F1" },
            new AccentOption { Name = "Arctic", Hex = "#38BDF8" },
            new AccentOption { Name = "Neon Pulse", Hex = "#A855F7" },
            new AccentOption { Name = "Crimson", Hex = "#EF4444" },
            new AccentOption { Name = "Personalizado", Hex = "#5CE07A" }
        ];

        WindowSizes =
        [
            new WindowSizeOption { Label = "1280 × 800", Width = 1280, Height = 800 },
            new WindowSizeOption { Label = "1440 × 900 (recomendado)", Width = 1440, Height = 900 },
            new WindowSizeOption { Label = "1600 × 960", Width = 1600, Height = 960 },
            new WindowSizeOption { Label = "1920 × 1080", Width = 1920, Height = 1080 }
        ];

        SystemInfo = systemInfo.GetSummary();

        AddBackgroundCommand = new RelayCommand(BrowseBackground);
        OpenDataFolderCommand = new RelayCommand(() => FileOps.OpenFolder(Paths.AppDataRoot));
        OpenLogsFolderCommand = new RelayCommand(() => FileOps.OpenFolder(Paths.LogsDirectory));
        ClearCacheCommand = new RelayCommand(ClearCache);
        ResetCommand = new RelayCommand(ResetSettings);
        OpenGraphicsSettingsCommand = new RelayCommand(() => FileOps.OpenUrl("ms-settings:display-advanced"));
        OpenTaskManagerCommand = new RelayCommand(() => FileOps.OpenUrl("taskmgr.exe"));
        RefreshSystemInfoCommand = new RelayCommand(RefreshSystemInfo);
        OpenStartupAppsCommand = new RelayCommand(() => FileOps.OpenUrl("ms-settings:startupapps"));
        OpenAboutLinkCommand = new RelayCommand(() => FileOps.OpenUrl("https://www.minecraft.net"));
    }

    public LauncherSettings Settings => _service.Settings;
    public ObservableCollection<BackgroundOption> Backgrounds { get; }
    public IReadOnlyList<AccentOption> Accents { get; }
    public IReadOnlyList<WindowSizeOption> WindowSizes { get; }
    public SystemSummary SystemInfo { get; private set; }
    public long LauncherMemoryBytes { get; private set; }
    public string LauncherMemoryText => $"{LauncherMemoryBytes / 1024d / 1024d:0.#} MB";

    public double LauncherMemoryPercent => SystemInfo.TotalRamBytes == 0
        ? 0
        : Math.Clamp(LauncherMemoryBytes * 100d / SystemInfo.TotalRamBytes, 0, 100);

    public event Action? AppearanceChanged;
    public event Action? WindowPlacementChanged;
    public event Action? ScaleChanged;

    public RelayCommand AddBackgroundCommand { get; }
    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenLogsFolderCommand { get; }
    public RelayCommand ClearCacheCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand OpenGraphicsSettingsCommand { get; }
    public RelayCommand OpenTaskManagerCommand { get; }
    public RelayCommand RefreshSystemInfoCommand { get; }
    public RelayCommand OpenStartupAppsCommand { get; }
    public RelayCommand OpenAboutLinkCommand { get; }

    public string Theme
    {
        get => Settings.Theme;
        set
        {
            if (!ThemeManager.ThemeNames.Contains(value, StringComparer.OrdinalIgnoreCase) || Settings.Theme == value)
                return;
            Settings.Theme = value;
            SaveAndNotify(nameof(Theme), nameof(IsDarkTheme), nameof(IsLightTheme), nameof(ThemeLabel));
            ApplyTheme();
            AppearanceChanged?.Invoke();
        }
    }

    public bool IsDarkTheme => !ThemeManager.IsLight(Theme);

    public bool IsLightTheme => ThemeManager.IsLight(Theme);

    public string ThemeLabel => Theme;

    public string Accent
    {
        get => Settings.Accent;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || Settings.Accent == value)
                return;
            Settings.Accent = value;
            SaveAndNotify(nameof(Accent));
            ApplyTheme();
            AppearanceChanged?.Invoke();
        }
    }

    public string CustomAccentHex
    {
        get => Settings.CustomAccentHex;
        set
        {
            var hex = string.IsNullOrWhiteSpace(value) ? "#5CE07A" : value.Trim();
            if (Settings.CustomAccentHex == hex)
                return;
            Settings.CustomAccentHex = hex;
            SaveAndNotify(nameof(CustomAccentHex));
            ApplyTheme();
        }
    }

    public string PreferredChannel
    {
        get => Settings.PreferredChannel;
        set
        {
            if (value is not ("Retail" or "Preview") || Settings.PreferredChannel == value)
                return;
            Settings.PreferredChannel = value;
            SaveAndNotify(nameof(PreferredChannel));
        }
    }

    public string JavaPlayerName
    {
        get => Settings.Java.PlayerName;
        set
        {
            var name = string.IsNullOrWhiteSpace(value) ? "Jugador" : value.Trim();
            if (Settings.Java.PlayerName == name)
                return;
            Settings.Java.PlayerName = name;
            SaveAndNotify(nameof(JavaPlayerName));
        }
    }

    public int JavaMemoryMb
    {
        get => Settings.Java.MemoryMb;
        set
        {
            var normalized = Math.Clamp(value, 1024, 32768);
            if (Settings.Java.MemoryMb == normalized)
                return;
            Settings.Java.MemoryMb = normalized;
            SaveAndNotify(nameof(JavaMemoryMb));
        }
    }

    public string JavaPath
    {
        get => Settings.Java.JavaPath;
        set
        {
            var path = value?.Trim() ?? string.Empty;
            if (Settings.Java.JavaPath == path)
                return;
            Settings.Java.JavaPath = path;
            SaveAndNotify(nameof(JavaPath));
        }
    }

    public string JavaGameDirectory
    {
        get => Settings.Java.GameDirectory;
        set
        {
            var path = value?.Trim() ?? string.Empty;
            if (Settings.Java.GameDirectory == path)
                return;
            Settings.Java.GameDirectory = path;
            SaveAndNotify(nameof(JavaGameDirectory));
        }
    }

    /// <summary>Applies the theme dictionary and the accent palette immediately.</summary>

    private void ApplyVisualSettings()
    {
        AnimationHelper.AnimationsEnabled = Settings.AnimationsEnabled && !Settings.PerformanceMode;
        AnimationHelper.AmbientEffectsEnabled = Settings.AmbientEffectsEnabled && !Settings.PerformanceMode;
        AnimationHelper.AmbientIntensity = Settings.AmbientIntensity;
        AnimationHelper.TransparencyLevel = Settings.TransparencyLevel;
        AnimationHelper.RespectSystemMotion = Settings.RespectSystemMotion;
        AnimationHelper.NavTransitionsEnabled = Settings.NavTransitionsEnabled && !Settings.PerformanceMode;
        AnimationHelper.ParticleEffectsEnabled = Settings.ParticleEffectsEnabled && !Settings.PerformanceMode;
        AnimationHelper.HoverEffectsEnabled = Settings.HoverEffectsEnabled;
        AnimationHelper.ParallaxEnabled = Settings.ParallaxEnabled && !Settings.PerformanceMode;
        AnimationHelper.CardAnimationsEnabled = Settings.CardAnimationsEnabled && !Settings.PerformanceMode;
        AnimationHelper.LoadingAnimationsEnabled = Settings.LoadingAnimationsEnabled && !Settings.PerformanceMode;
        AnimationHelper.MotionLevel = Settings.MotionLevel;
        AnimationHelper.PerformanceMode = Settings.PerformanceMode;
        ThemeManager.ApplyTransparency(Settings.TransparencyLevel);
    }

    private void ApplyTheme()
    {
        try
        {
            ThemeManager.SetCustomAccent(Settings.CustomAccentHex);
            ThemeManager.Apply(Settings.Theme, Settings.Accent);
            LogService.Info($"Tema aplicado: {Settings.Theme} · acento {Settings.Accent}.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo aplicar el tema en caliente.");
        }
    }

    public string BackgroundId
    {
        get => Settings.BackgroundId;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || Settings.BackgroundId == value)
                return;
            Settings.BackgroundId = value;
            SaveAndNotify(nameof(BackgroundId), nameof(SelectedBackground), nameof(BackgroundPath));
            AppearanceChanged?.Invoke();
        }
    }

    public BackgroundOption? SelectedBackground
    {
        get => Backgrounds.FirstOrDefault(b => b.Id == Settings.BackgroundId) ?? Backgrounds.FirstOrDefault();
        set
        {
            if (value is null)
                return;
            BackgroundId = value.Id;
        }
    }

    public string? BackgroundPath => Paths.GetBackgroundPath(Settings.BackgroundId, Settings.CustomBackgroundPath);

    public string WindowSizeLabel
    {
        get
        {
            var match = WindowSizes.FirstOrDefault(w => w.Width == Settings.WindowWidth && w.Height == Settings.WindowHeight);
            return match?.Label ?? $"{Settings.WindowWidth} × {Settings.WindowHeight}";
        }
        set
        {
            var match = WindowSizes.FirstOrDefault(w => w.Label == value);
            if (match is null)
                return;
            Settings.WindowWidth = match.Width;
            Settings.WindowHeight = match.Height;
            SaveAndNotify(nameof(WindowSizeLabel));
            WindowPlacementChanged?.Invoke();
        }
    }

    public double UiScale
    {
        get => Settings.UiScale;
        set
        {
            var normalized = Math.Round(Math.Clamp(value, 0.85, 1.15), 2);
            if (Math.Abs(Settings.UiScale - normalized) < 0.001)
                return;
            Settings.UiScale = normalized;
            SaveAndNotify(nameof(UiScale), nameof(UiScaleLabel));
            ScaleChanged?.Invoke();
        }
    }

    public string UiScaleLabel => $"{Settings.UiScale * 100:0} %";

    public bool MinimizeOnLaunch
    {
        get => Settings.MinimizeOnLaunch;
        set { Settings.MinimizeOnLaunch = value; SaveAndNotify(nameof(MinimizeOnLaunch)); }
    }

    public bool CloseOnLaunch
    {
        get => Settings.CloseOnLaunch;
        set { Settings.CloseOnLaunch = value; SaveAndNotify(nameof(CloseOnLaunch)); }
    }

    public bool VerifyBeforeLaunch
    {
        get => Settings.VerifyBeforeLaunch;
        set { Settings.VerifyBeforeLaunch = value; SaveAndNotify(nameof(VerifyBeforeLaunch)); }
    }

    public bool AutoCheckUpdates
    {
        get => Settings.AutoCheckUpdates;
        set { Settings.AutoCheckUpdates = value; SaveAndNotify(nameof(AutoCheckUpdates)); }
    }

    /// <summary>Interval between update checks./
    public TimeSpan UpdateCheckInterval
    {
        get => Settings.UpdateCheckInterval;
        set { Settings.UpdateCheckInterval = value; SaveAndNotify(nameof(UpdateCheckInterval)); }
    }

    /// <summary>Whether to show update notifications./
    public bool ShowUpdateNotifications
    {
        get => Settings.ShowUpdateNotifications;
        set { Settings.ShowUpdateNotifications = value; SaveAndNotify(nameof(ShowUpdateNotifications)); }
    }

    /// <summary>Last check time timestamp./
    public DateTimeOffset LastCheckTime
    {
        get => Settings.LastCheckTime;
        set { Settings.LastCheckTime = value; SaveAndNotify(nameof(LastCheckTime)); }
    }
    public bool ShowNews
    {
        get => Settings.ShowNews;
        set { Settings.ShowNews = value; SaveAndNotify(nameof(ShowNews)); }
    }

    public IReadOnlyList<string> MotionLevels => AnimationHelper.MotionLevels;

    public double BackgroundBlur
    {
        get => Settings.BackgroundBlur;
        set
        {
            var clamped = Math.Clamp(value, 0, 8);
            if (Math.Abs(Settings.BackgroundBlur - clamped) < 0.01)
                return;
            Settings.BackgroundBlur = clamped;
            SaveAndNotify(nameof(BackgroundBlur));
        }
    }

    public double BackgroundOverlayIntensity
    {
        get => Settings.BackgroundOverlayIntensity;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (Math.Abs(Settings.BackgroundOverlayIntensity - clamped) < 0.01)
                return;
            Settings.BackgroundOverlayIntensity = clamped;
            SaveAndNotify(nameof(BackgroundOverlayIntensity));
        }
    }

    public string MotionLevel
    {
        get => Settings.MotionLevel;
        set
        {
            if (string.Equals(Settings.MotionLevel, value, StringComparison.OrdinalIgnoreCase))
                return;
            Settings.MotionLevel = value;
            AnimationHelper.MotionLevel = value;
            SaveAndNotify(nameof(MotionLevel), nameof(MotionLevelLabel));
        }
    }

    public string MotionLevelLabel => Settings.MotionLevel;

    public bool AnimationsEnabled
    {
        get => Settings.AnimationsEnabled;
        set
        {
            if (Settings.AnimationsEnabled == value)
                return;
            Settings.AnimationsEnabled = value;
            AnimationHelper.AnimationsEnabled = value;
            SaveAndNotify(nameof(AnimationsEnabled), nameof(AnimationsEnabledLabel));
        }
    }

    public string AnimationsEnabledLabel => Settings.AnimationsEnabled ? "Activadas" : "Desactivadas";

    public bool AmbientEffectsEnabled
    {
        get => Settings.AmbientEffectsEnabled;
        set
        {
            if (Settings.AmbientEffectsEnabled == value)
                return;
            Settings.AmbientEffectsEnabled = value;
            AnimationHelper.AmbientEffectsEnabled = value;
            SaveAndNotify(nameof(AmbientEffectsEnabled), nameof(AmbientEffectsEnabledLabel));
        }
    }

    public string AmbientEffectsEnabledLabel => Settings.AmbientEffectsEnabled ? "Activados" : "Desactivados";

    public double AmbientIntensity
    {
        get => Settings.AmbientIntensity;
        set
        {
            var v = Math.Clamp(value, 0, 1);
            if (Math.Abs(Settings.AmbientIntensity - v) < 0.001) return;
            Settings.AmbientIntensity = v;
            ApplyVisualSettings();
            SaveAndNotify(nameof(AmbientIntensity));
        }
    }

    public double TransparencyLevel
    {
        get => Settings.TransparencyLevel;
        set
        {
            var v = Math.Clamp(value, 0, 1);
            if (Math.Abs(Settings.TransparencyLevel - v) < 0.001) return;
            Settings.TransparencyLevel = v;
            ApplyVisualSettings();
            SaveAndNotify(nameof(TransparencyLevel));
        }
    }

    public bool RespectSystemMotion
    {
        get => Settings.RespectSystemMotion;
        set { Settings.RespectSystemMotion = value; ApplyVisualSettings(); SaveAndNotify(nameof(RespectSystemMotion)); }
    }

    public bool NavTransitionsEnabled
    {
        get => Settings.NavTransitionsEnabled;
        set { Settings.NavTransitionsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(NavTransitionsEnabled)); }
    }

    public bool ParticleEffectsEnabled
    {
        get => Settings.ParticleEffectsEnabled;
        set { Settings.ParticleEffectsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(ParticleEffectsEnabled)); }
    }

    public bool HoverEffectsEnabled
    {
        get => Settings.HoverEffectsEnabled;
        set { Settings.HoverEffectsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(HoverEffectsEnabled)); }
    }

    public bool ParallaxEnabled
    {
        get => Settings.ParallaxEnabled;
        set { Settings.ParallaxEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(ParallaxEnabled)); }
    }

    public bool CardAnimationsEnabled
    {
        get => Settings.CardAnimationsEnabled;
        set { Settings.CardAnimationsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(CardAnimationsEnabled)); }
    }

    public bool LoadingAnimationsEnabled
    {
        get => Settings.LoadingAnimationsEnabled;
        set { Settings.LoadingAnimationsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(LoadingAnimationsEnabled)); }
    }

    public bool PerformanceMode
    {
        get => Settings.PerformanceMode;
        set
        {
            Settings.PerformanceMode = value;
            if (value)
            {
                Settings.ParticleEffectsEnabled = false;
                Settings.ParallaxEnabled = false;
                Settings.CardAnimationsEnabled = false;
                Settings.AmbientEffectsEnabled = false;
            }
            ApplyVisualSettings();
            SaveAndNotify(nameof(PerformanceMode), nameof(ParticleEffectsEnabled),
                nameof(ParallaxEnabled), nameof(CardAnimationsEnabled), nameof(AmbientEffectsEnabled));
        }
    }

    public string? GetBackgroundPathForSection(string section)
    {
        if (Settings.BackgroundId == "custom" && !string.IsNullOrWhiteSpace(Settings.CustomBackgroundPath))
            return Settings.CustomBackgroundPath;

        var id = section switch
        {
            "java" => "cueva",
            "bedrock" => "ciudad",
            "versions" => "amanecer",
            "addons" => "cerezo",
            "mods" => "magico",
            "downloads" => "aurora",
            "settings" => "fantastico",
            "profile" => "end",
            "mobile" => "nocturno",
            _ => Settings.BackgroundId
        };
        return Paths.GetBuiltInBackground(id) ?? Paths.GetBuiltInBackground("bosque");
    }

    public bool StartWithWindows
    {
        get => Settings.StartWithWindows;
        set
        {
            if (Settings.StartWithWindows == value)
                return;

            if (!StartupService.SetEnabled(value, out var message))
            {
                ToastHub.Error(message);
                OnPropertyChanged();
                return;
            }

            Settings.StartWithWindows = value;
            SaveAndNotify(nameof(StartWithWindows));
            ToastHub.Success(message);
        }
    }

    public string LocalProfileName
    {
        get => Settings.LocalProfileName;
        set
        {
            var name = string.IsNullOrWhiteSpace(value) ? "Jugador" : value.Trim();
            if (name.Length > 32)
                name = name[..32];
            if (Settings.LocalProfileName == name)
                return;
            Settings.LocalProfileName = name;
            SaveAndNotify(nameof(LocalProfileName));
        }
    }

    public string MicrosoftClientId
    {
        get => Settings.MicrosoftClientId;
        set
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (Settings.MicrosoftClientId == trimmed)
                return;
            Settings.MicrosoftClientId = trimmed;
            SaveAndNotify(nameof(MicrosoftClientId), nameof(IsMicrosoftConfigured));
        }
    }

    public bool IsMicrosoftConfigured => !string.IsNullOrWhiteSpace(Settings.MicrosoftClientId);

    public bool AutoRestoreAccount
    {
        get => Settings.AutoRestoreAccount;
        set { Settings.AutoRestoreAccount = value; SaveAndNotify(nameof(AutoRestoreAccount)); }
    }

    public void ApplyInitial()
    {
        _isApplying = true;
        try
        {
            ApplyVisualSettings();
            ThemeManager.SetCustomAccent(Settings.CustomAccentHex);
            ThemeManager.Apply(Settings.Theme, Settings.Accent);

            var actualStartup = StartupService.IsEnabled();
            if (Settings.StartWithWindows != actualStartup)
            {
                Settings.StartWithWindows = actualStartup;
                _service.Save();
            }

            RefreshSystemInfo();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudieron aplicar los ajustes iniciales.");
        }
        finally
        {
            _isApplying = false;
        }
    }

    public void Save()
    {
        _service.Save();
    }

    public void RefreshSystemInfo()
    {
        SystemInfo = _systemInfo.GetSummary();
        LauncherMemoryBytes = _systemInfo.GetLauncherMemoryBytes();
        OnPropertyChanged(nameof(SystemInfo));
        OnPropertyChanged(nameof(LauncherMemoryBytes));
        OnPropertyChanged(nameof(LauncherMemoryText));
        OnPropertyChanged(nameof(LauncherMemoryPercent));
    }

    private void BrowseBackground()
    {
        var file = _dialogs.PickImage("Elige una imagen de fondo");
        if (file is null)
            return;

        try
        {
            Directory.CreateDirectory(Paths.BackgroundsDirectory);
            var extension = Path.GetExtension(file);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 6)
                extension = ".png";
            var destination = Path.Combine(Paths.BackgroundsDirectory, $"custom-{DateTime.Now:yyyyMMddHHmmss}{extension}");
            File.Copy(file, destination, overwrite: true);

            var previous = Settings.CustomBackgroundPath;
            Settings.CustomBackgroundPath = destination;
            Settings.BackgroundId = "custom";
            SaveAndNotify(nameof(BackgroundId), nameof(SelectedBackground), nameof(BackgroundPath));
            AddCustomBackgroundIfPresent();
            AppearanceChanged?.Invoke();
            ToastHub.Success("Fondo personalizado aplicado.");

            if (!string.IsNullOrWhiteSpace(previous) && File.Exists(previous) &&
                !previous.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(previous); } catch { /* not critical */ }
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo aplicar el fondo personalizado.");
            ToastHub.Error("No se pudo copiar la imagen de fondo.");
        }
    }

    private void AddCustomBackgroundIfPresent()
    {
        if (string.IsNullOrWhiteSpace(Settings.CustomBackgroundPath) ||
            !File.Exists(Settings.CustomBackgroundPath))
            return;

        var existing = Backgrounds.FirstOrDefault(b => b.Id == "custom");
        if (existing is not null)
            Backgrounds.Remove(existing);

        Backgrounds.Add(new BackgroundOption
        {
            Id = "custom",
            Name = "Imagen personalizada",
            Path = Settings.CustomBackgroundPath
        });
        OnPropertyChanged(nameof(SelectedBackground));
    }

    private void ClearCache()
    {
        if (!_dialogs.Confirm("Limpiar caché",
                "Se eliminarán las imágenes y miniaturas descargadas. No se tocarán los complementos instalados, tu cuenta ni los ajustes.",
                "Limpiar caché"))
            return;

        try
        {
            foreach (var directory in new[] { Paths.ImagesCacheDirectory, Paths.AddonCacheDirectory })
            {
                if (!Directory.Exists(directory))
                    continue;
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    try { File.Delete(file); } catch { /* locked file */ }
                }
            }
            ToastHub.Success("Caché del launcher eliminada.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo limpiar la caché.");
            ToastHub.Error("No se pudo limpiar toda la caché.");
        }
    }

    private void ResetSettings()
    {
        if (!_dialogs.Confirm("Restablecer ajustes",
                "Se restaurarán el tema, el fondo y las opciones de inicio predeterminadas. La cuenta y los complementos no se modifican.",
                "Restablecer", danger: true))
            return;

        StartupService.SetEnabled(false, out _);
        _service.Reset();
        ApplyVisualSettings();
        ThemeManager.Apply(Settings.Theme, Settings.Accent);
        OnPropertyChanged(string.Empty);
        AppearanceChanged?.Invoke();
        ScaleChanged?.Invoke();
        WindowPlacementChanged?.Invoke();
        RefreshSystemInfo();
        ToastHub.Success("Ajustes restablecidos.");
    }

    private void SaveAndNotify(params string[] properties)
    {
        if (!_isApplying)
            _service.Save();

        foreach (var property in properties)
            OnPropertyChanged(property);

        OnPropertyChanged(nameof(Settings));
    }
}
