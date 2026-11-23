using XdLauncher.Core;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

public sealed class SettingsCategory
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Glyph { get; init; }
    public required string Description { get; init; }
}

/// <summary>Launcher settings page facade.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private XdAccount _account;
    private string _selectedCategory = "appearance";

    public SettingsViewModel(SettingsState state, ProfileState profile, XdAccount account)
    {
        State = state;
        Profile = profile;
        _account = account;

        OpenAzureCommand = new RelayCommand(() => FileOps.OpenUrl("https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade"));
        OpenLogsCommand = new RelayCommand(() => FileOps.OpenFolder(Paths.LogsDirectory));
        OpenReadmeCommand = new RelayCommand(() =>
        {
            var readme = Path.Combine(AppContext.BaseDirectory, "README.md");
            if (File.Exists(readme))
                FileOps.ShowInExplorer(readme);
            else
                FileOps.OpenFolder(AppContext.BaseDirectory);
        });
        OpenWebsiteCommand = new RelayCommand(() => FileOps.OpenUrl(AppInfo.Website));
        CopyDiagnosticsCommand = new RelayCommand(CopyDiagnostics);
        RefreshInfoCommand = new RelayCommand(state.RefreshSystemInfo);
        SelectCategoryCommand = new RelayCommand<string>(key =>
        {
            if (!string.IsNullOrWhiteSpace(key))
                SelectedCategory = key;
        });
        OpenDataFolderCommand = new RelayCommand(() => FileOps.OpenFolder(Paths.AppDataRoot));
        OpenDownloadsFolderCommand = new RelayCommand(() => FileOps.OpenFolder(Paths.DownloadsDirectory));
        OpenJavaWorkspaceCommand = new RelayCommand(() => FileOps.OpenFolder(Paths.CreateNoPremiumJavaWorkspace().Root));

        Categories =
        [
            new SettingsCategory { Key = "appearance", Title = "Apariencia", Glyph = "\uE790", Description = "Identidad y composición" },
            new SettingsCategory { Key = "themes", Title = "Temas", Glyph = "\uE790", Description = "Paletas y acentos" },
            new SettingsCategory { Key = "backgrounds", Title = "Fondos", Glyph = "\uE91B", Description = "Ambientes cinematográficos" },
            new SettingsCategory { Key = "motion", Title = "Animaciones", Glyph = "\uE9F5", Description = "Movimiento y efectos" },
            new SettingsCategory { Key = "interface", Title = "Interfaz", Glyph = "\uE7F8", Description = "Escala y ventana" },
            new SettingsCategory { Key = "account", Title = "Cuenta", Glyph = "\uE77B", Description = "Perfil y sesión" },
            new SettingsCategory { Key = "minecraft", Title = "Minecraft", Glyph = "\uE7FC", Description = "Juego y canales" },
            new SettingsCategory { Key = "java", Title = "Java", Glyph = "\uE945", Description = "Versiones y memoria" },
            new SettingsCategory { Key = "downloads", Title = "Descargas", Glyph = "\uE896", Description = "Archivos y progreso" },
            new SettingsCategory { Key = "performance", Title = "Rendimiento", Glyph = "\uE9C6", Description = "Equipo y memoria" },
            new SettingsCategory { Key = "system", Title = "Sistema", Glyph = "\uE7FC", Description = "Inicio y actualizaciones" },
            new SettingsCategory { Key = "diagnostics", Title = "Diagnóstico", Glyph = "\uE9D9", Description = "Registros y soporte" },
            new SettingsCategory { Key = "privacy", Title = "Privacidad", Glyph = "\uE72E", Description = "Datos y seguridad" },
            new SettingsCategory { Key = "about", Title = "Acerca de", Glyph = "\uE946", Description = "Versión y recursos" }
        ];
    }

    public SettingsState State { get; }
    public ProfileState Profile { get; }
    public XdAccount Account => _account;
    public IReadOnlyList<SettingsCategory> Categories { get; }
    public IReadOnlyList<string> ThemeOptions => ThemeManager.ThemeNames;

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public string SelectedCategoryTitle
        => Categories.FirstOrDefault(c => c.Key == _selectedCategory)?.Title ?? "Ajustes";

    public RelayCommand<string> SelectCategoryCommand { get; }
    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenDownloadsFolderCommand { get; }
    public RelayCommand OpenJavaWorkspaceCommand { get; }

    public void UpdateAccount(XdAccount account)
    {
        _account = account;
        OnPropertyChanged(nameof(Account));
        OnPropertyChanged(nameof(AccountDisplayName));
        OnPropertyChanged(nameof(AccountEmail));
        OnPropertyChanged(nameof(AccountInitial));
        OnPropertyChanged(nameof(AccountAvatarPath));
        OnPropertyChanged(nameof(AccountProviderLabel));
    }

    public string AccountDisplayName => Account.DisplayName ?? Account.Email;
    public string AccountEmail => Account.Email;
    public string AccountInitial => Account.Initial;
    public string? AccountAvatarPath => Account.AvatarPath;
    public string AccountProviderLabel => Account.ProviderLabel;

    public string LauncherVersion => AppInfo.FullVersion;
    public string ProductName => AppInfo.ProductName;
    public string Website => AppInfo.Website;
    public string RuntimeLabel => $".NET {Environment.Version} · {Environment.OSVersion.VersionString}";
    public string MemoryFootprint => State.LauncherMemoryText;

    public RelayCommand OpenAzureCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand OpenReadmeCommand { get; }
    public RelayCommand OpenWebsiteCommand { get; }
    public RelayCommand CopyDiagnosticsCommand { get; }
    public RelayCommand RefreshInfoCommand { get; }

    private void CopyDiagnostics()
    {
        var info = State.SystemInfo;
        var text =
            $"{AppInfo.ProductName} {AppInfo.FullVersion}{Environment.NewLine}" +
            $"Windows: {info.WindowsName} ({info.OsArchitecture}){Environment.NewLine}" +
            $"CPU: {info.CpuName}{Environment.NewLine}" +
            $"GPU: {info.GpuName}{Environment.NewLine}" +
            $"RAM: {info.TotalRamText} totales · {info.AvailableRamText} disponibles · {info.RamUsageText}{Environment.NewLine}" +
            $"Launcher en memoria: {State.LauncherMemoryText}{Environment.NewLine}" +
            $"Registro: {LogService.CurrentFile ?? Paths.LogsDirectory}";
        FileOps.CopyText(text);
    }
}
