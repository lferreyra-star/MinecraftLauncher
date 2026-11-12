using XdLauncher.Core;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Launcher settings page facade.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(SettingsState state, ProfileState profile, XdAccount account)
    {
        State = state;
        Profile = profile;
        Account = account;

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
    }

    public SettingsState State { get; }
    public ProfileState Profile { get; }
    public XdAccount Account { get; }

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
