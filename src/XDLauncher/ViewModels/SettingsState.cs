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
            new() { Id = "bosque", Name = "Bosque profundo", Path = Paths.GetBuiltInBackground("bosque") ?? string.Empty },
            new() { Id = "aurora", Name = "Aurora boreal", Path = Paths.GetBuiltInBackground("aurora") ?? string.Empty },
            new() { Id = "amanecer", Name = "Amanecer", Path = Paths.GetBuiltInBackground("amanecer") ?? string.Empty },
            new() { Id = "cueva", Name = "Cueva de cristal", Path = Paths.GetBuiltInBackground("cueva") ?? string.Empty }
        };
        AddCustomBackgroundIfPresent();

        Accents =
        [
            new AccentOption { Name = "Verde", Hex = "#5CE07A" },
            new AccentOption { Name = "Esmeralda", Hex = "#2DD4A7" },
            new AccentOption { Name = "Lima", Hex = "#B4E64B" },
            new AccentOption { Name = "Cian", Hex = "#45C8F5" }
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
            if (value is not ("Dark" or "Light") || Settings.Theme == value)
                return;
            Settings.Theme = value;
            SaveAndNotify(nameof(Theme), nameof(IsDarkTheme), nameof(IsLightTheme), nameof(ThemeLabel));
            ApplyTheme();
            AppearanceChanged?.Invoke();
        }
    }

    public bool IsDarkTheme
    {
        get => Theme == "Dark";
        set { if (value) Theme = "Dark"; }
    }

    public bool IsLightTheme
    {
        get => Theme == "Light";
        set { if (value) Theme = "Light"; }
    }

    public string ThemeLabel => Theme == "Light" ? "Claro" : "Oscuro";

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

    /// <summary>Applies the theme dictionary and the accent palette immediately.</summary>
    private void ApplyTheme()
    {
        try
        {
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
