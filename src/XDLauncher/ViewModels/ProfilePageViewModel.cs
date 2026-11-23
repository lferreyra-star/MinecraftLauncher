using System.ComponentModel;
using XdLauncher.Core;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Dedicated profile page: XD account, Minecraft identity and quick actions.</summary>
public sealed class ProfilePageViewModel : ObservableObject
{
    private readonly Action _openSettings;
    private readonly SettingsViewModel _settingsPage;
    private XdAccount _account;

    public ProfilePageViewModel(
        ProfileState profile,
        SettingsState settings,
        SettingsViewModel settingsPage,
        XdAccount account,
        Action openSettings)
    {
        Profile = profile;
        Settings = settings;
        _settingsPage = settingsPage;
        _account = account;
        _openSettings = openSettings;

        OpenAccountSettingsCommand = new RelayCommand(() =>
        {
            _settingsPage.SelectedCategory = "account";
            _openSettings();
        });
        CopyUserIdCommand = new RelayCommand(() => FileOps.CopyText(Account.UserId));
        OpenWebsiteCommand = new RelayCommand(() => FileOps.OpenUrl(AppInfo.Website));

        Profile.PropertyChanged += OnProfileChanged;
    }

    public ProfileState Profile { get; }
    public SettingsState Settings { get; }

    public XdAccount Account => _account;
    public string DisplayName => string.IsNullOrWhiteSpace(Account.DisplayName) ? Account.Email : Account.DisplayName!;
    public string Email => Account.Email;
    public string Initial => Account.Initial;
    public string ProviderLabel => Account.ProviderLabel;
    public string? AvatarPath => Account.AvatarPath;
    public string UserId => Account.UserId;
    public string UserIdShort => Account.UserId.Length <= 12 ? Account.UserId : $"{Account.UserId[..6]}…{Account.UserId[^4..]}";
    public string AccountTypeLabel => Profile.AccountTypeLabel;
    public string Gamertag => Profile.DisplayName;
    public string GamerscoreLabel => Profile.GamerscoreLabel;
    public string MinecraftInitial => Profile.Initial;
    public bool IsMicrosoftAccount => Profile.IsMicrosoftAccount;
    public bool IsLocal => Profile.IsLocal;
    public string SyncStatus => Profile.StatusMessage;
    public string LauncherVersion => AppInfo.FullVersion;
    public string SessionStateLabel => "Sesión activa · Firebase";

    public RelayCommand OpenAccountSettingsCommand { get; }
    public RelayCommand CopyUserIdCommand { get; }
    public RelayCommand OpenWebsiteCommand { get; }

    public void UpdateAccount(XdAccount account)
    {
        _account = account;
        OnPropertyChanged(nameof(Account));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(ProviderLabel));
        OnPropertyChanged(nameof(AvatarPath));
        OnPropertyChanged(nameof(UserId));
        OnPropertyChanged(nameof(UserIdShort));
    }

    private void OnProfileChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AccountTypeLabel));
        OnPropertyChanged(nameof(Gamertag));
        OnPropertyChanged(nameof(GamerscoreLabel));
        OnPropertyChanged(nameof(MinecraftInitial));
        OnPropertyChanged(nameof(IsMicrosoftAccount));
        OnPropertyChanged(nameof(IsLocal));
        OnPropertyChanged(nameof(SyncStatus));
    }
}
