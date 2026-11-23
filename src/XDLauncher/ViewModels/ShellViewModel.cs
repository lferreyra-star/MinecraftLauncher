using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly DialogService _dialogs;
    private readonly UpdateService _updateService;
    private readonly UpdateCheckService _updateCheckService;
    private XdAccount _account;
    private NavigationItem _currentNavigation;
    private bool _isInitializing;
    private bool _isFullscreen;
    private bool _isSidebarCollapsed;
    private int _lastNavIndex;
    private int _navigationDirection = 1;
    private string _initializationStatus = "Preparando el launcher…";

    // Almacenar la última información de actualización verificada
    private UpdateInfo? _lastUpdateInfo;

    public ShellViewModel(
        SettingsService settingsService,
        GameService gameService,
        AuthService authService,
        AddonService addonService,
        StoreCatalogService catalogService,
        SystemInfoService systemInfo,
        DialogService dialogs,
        MinecraftDownloadService downloadService,
        UpdateService updateService,
        UpdateCheckService updateCheckService,
        XdAccount account)
    {
        _dialogs = dialogs;
        _updateService = updateService;
        _updateCheckService = updateCheckService;
        _account = account;

        _updateService.UpdateChecked += OnUpdateChecked;
        _updateService.UpdateAvailable += OnUpdateAvailable;

        _updateCheckService.UpdateChecked += OnUpdateChecked;
        _updateCheckService.UpdateAvailable += OnUpdateAvailable;
        _updateCheckService.StatusMessage += OnStatusMessage;

        Settings = new SettingsState(settingsService, dialogs, systemInfo);
        Catalog = new CatalogState(catalogService);
        Game = new GameState(gameService, Catalog, Settings, dialogs);
        Profile = new ProfileState(authService, Settings, dialogs);
        MobileState = new MobileConnectState(settingsService, dialogs);
        Home = new HomeViewModel(Game, Catalog, Profile, Settings, MobileState, account);
        Versions = new VersionsViewModel(Game, Catalog);
        Addons = new AddonsViewModel(addonService, Game, Settings, dialogs);
        Downloads = new DownloadViewModel(downloadService, dialogs);
        Java = new JavaViewModel(Settings, Profile, dialogs);
        Home.JavaPage = Java;
        Mods = new ModsCenterViewModel(Java, dialogs);
        SettingsPage = new SettingsViewModel(Settings, Profile, account);
        ProfilePage = new ProfilePageViewModel(Profile, Settings, SettingsPage, account, () => Navigate("settings"));


        Navigation =
        [
            new NavigationItem
            {
                Key = "home", Title = "Inicio", Glyph = "", Group = "Jugar",
                Subtitle = "Tu centro de mando", Page = Home
            },
            new NavigationItem
            {
                Key = "java", Title = "Minecraft Java", Glyph = "", Group = "Jugar",
                Subtitle = "Versiones, Java y lanzamiento", Page = Java
            },
            new NavigationItem
            {
                Key = "bedrock", Title = "Minecraft Bedrock", Glyph = "", Group = "Jugar",
                Subtitle = "Instalación, componentes y estado", Page = Game
            },
            new NavigationItem
            {
                Key = "game", Title = "Juego", Glyph = "", Group = "Jugar",
                Subtitle = "Estado, componentes y archivos", Page = Game
            },
            new NavigationItem
            {
                Key = "mobile", Title = "Móvil", Glyph = "", Group = "Jugar",
                Subtitle = "Conecta tu teléfono Android", Page = MobileState
            },
            new NavigationItem
            {
                Key = "versions", Title = "Versiones", Glyph = "", Group = "Biblioteca",
                Subtitle = "Actualizaciones oficiales y notas", Page = Versions
            },
            new NavigationItem
            {
                Key = "addons", Title = "Complementos", Glyph = "", Group = "Biblioteca",
                Subtitle = "Addons, texturas, skins y mapas", Page = Addons
            },
            new NavigationItem
            {
                Key = "mods", Title = "Centro de Mods", Glyph = "", Group = "Biblioteca",
                Subtitle = "Modrinth · dependencias y biblioteca", Page = Mods
            },
            new NavigationItem
            {
                Key = "downloads", Title = "Descargas", Glyph = "", Group = "Biblioteca",
                Subtitle = "Descarga Minecraft y herramientas", Page = Downloads
            },
            new NavigationItem
            {
                Key = "profile", Title = "Perfil", Glyph = "", Group = "Sistema",
                Subtitle = "Cuenta, identidad y sesión", Page = ProfilePage
            },
            new NavigationItem
            {
                Key = "settings", Title = "Ajustes", Glyph = "", Group = "Sistema",
                Subtitle = "Personalización y sistema", Page = SettingsPage
            }
        ];

        var quickKeys = new[] { "home", "java", "bedrock", "versions", "mods", "downloads", "settings" };
        QuickNavigation = Navigation.Where(item => quickKeys.Contains(item.Key)).ToList();
        _currentNavigation = Navigation[0];
        Toasts = [];

        NavigateCommand = new RelayCommand(parameter =>
        {
            var key = parameter switch
            {
                NavigationItem item => item.Key,
                string value => value,
                _ => null
            };
            if (key is not null)
                Navigate(key);
        });
        DismissToastCommand = new RelayCommand(parameter =>
        {
            if (parameter is ToastMessage toast)
                Toasts.Remove(toast);
        });
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);
        OpenCommandPaletteCommand = new RelayCommand(() => CommandPaletteRequested?.Invoke());
        SignOutCommand = new RelayCommand(() =>
        {
            XdSessionStore.Delete();
            LogService.Info("Cierre de sesión de XD Launcher.");
            SignOutRequested?.Invoke();
        });

        ToastHub.ToastRequested += OnToastRequested;
        Game.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(GameStatusText));
            OnPropertyChanged(nameof(GameStatusDetail));
            OnPropertyChanged(nameof(BedrockStatusText));
            OnPropertyChanged(nameof(BedrockStatusDetail));
            OnPropertyChanged(nameof(BedrockStatusGlyph));
            OnPropertyChanged(nameof(IsGameRunning));
            OnPropertyChanged(nameof(GameStatusGlyph));
        };
        Java.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(GameStatusText));
            OnPropertyChanged(nameof(GameStatusDetail));
            OnPropertyChanged(nameof(GameStatusGlyph));
        };
        Profile.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(AccountStatusText));
            OnPropertyChanged(nameof(DisplayName));
        };
        Game.Launched += OnGameLaunched;
        Settings.AppearanceChanged += () =>
        {
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(BackdropSource));
            OnPropertyChanged(nameof(CurrentBackgroundPath));
        };
        MobileState.NotificationRequested += (message, isError) =>
        {
            if (isError)
                ToastHub.Error(message);
            else
                ToastHub.Success(message);
        };
        MobileState.FullscreenChanged += OnFullscreenChanged;
        Home.OpenMobileRequested += () => Navigate("mobile");
        Home.OpenJavaRequested += () => Navigate("java");
    }

    public SettingsState Settings { get; }
    public CatalogState Catalog { get; }
    public GameState Game { get; }
    public ProfileState Profile { get; }
    public XdAccount Account => _account;
    public HomeViewModel Home { get; }
    public VersionsViewModel Versions { get; }
    public AddonsViewModel Addons { get; }
    public ModsCenterViewModel Mods { get; }
    public DownloadViewModel Downloads { get; }
    public JavaViewModel Java { get; }
    public SettingsViewModel SettingsPage { get; }
    public ProfilePageViewModel ProfilePage { get; }
    public MobileConnectState MobileState { get; }
    public IReadOnlyList<NavigationItem> Navigation { get; }
    public IReadOnlyList<NavigationItem> QuickNavigation { get; private set; } = [];
    public ObservableCollection<ToastMessage> Toasts { get; }

    public NavigationItem CurrentNavigation
    {
        get => _currentNavigation;
        set
        {
            if (value is null || SetProperty(ref _currentNavigation, value))
            {
                var newIndex = value is null ? 0 : Navigation.ToList().IndexOf(value);
                _navigationDirection = newIndex >= _lastNavIndex ? 1 : -1;
                _lastNavIndex = newIndex;
                OnPropertyChanged(nameof(NavigationDirection));
                OnPropertyChanged(nameof(CurrentBackgroundPath));
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(CurrentTitle));
                OnPropertyChanged(nameof(CurrentSubtitle));
                OnPropertyChanged(nameof(CurrentGlyph));
                OnPropertyChanged(nameof(CurrentGroup));
                OnPropertyChanged(nameof(CurrentNavigationKey));
                if (value is not null)
                    OnNavigated(value.Key);
            }
        }
    }

    public string CurrentNavigationKey
    {
        get => CurrentNavigation?.Key ?? "home";
        set
        {
            if (!string.Equals(value, CurrentNavigation?.Key, StringComparison.OrdinalIgnoreCase))
                Navigate(value);
        }
    }

    public object CurrentPage => CurrentNavigation.Page;
    public string CurrentTitle => CurrentNavigation.Title;
    public string CurrentSubtitle => CurrentNavigation.Subtitle;
    public string CurrentGlyph => CurrentNavigation.Glyph;
    public string CurrentGroup => CurrentNavigation.Group;
    public int NavigationDirection => _navigationDirection;
    public string? CurrentBackgroundPath => Settings.GetBackgroundPathForSection(CurrentNavigation?.Key ?? "home");

    public bool IsInitializing
    {
        get => _isInitializing;
        private set
        {
            if (SetProperty(ref _isInitializing, value))
                OnPropertyChanged(nameof(IsReady));
        }
    }

    public bool IsReady => !IsInitializing;

    public string InitializationStatus
    {
        get => _initializationStatus;
        private set => SetProperty(ref _initializationStatus, value);
    }

    public string DisplayName => Profile.DisplayName;
    public string TopBarStatusText => IsInitializing ? InitializationStatus : GameStatusText;
    public string TopBarActivityText => Game.IsGameRunning ? "Minecraft en ejecución" : "Sesión lista";
    public string AccountStatusText => Profile.IsMicrosoftAccount
        ? $"Xbox Live · {Profile.DisplayName}"
        : "Perfil local";
    public string BedrockStatusText => Game.IsInstalled
        ? Game.IsGameRunning ? "Minecraft en ejecución" : $"Listo · {Game.VersionLabel}"
        : "Minecraft no instalado";
    public string BedrockStatusDetail => Game.IsInstalled
        ? $"{Game.DisplayName} · {Game.ChannelLabel}"
        : "Instálalo desde Microsoft Store";
    public string BedrockStatusGlyph => Game.IsInstalled ? "\uE73E" : "\uEA39";

    public string GameStatusText => CurrentNavigation?.Key == "java"
        ? "Minecraft Java Edition"
        : BedrockStatusText;
    public string GameStatusDetail => CurrentNavigation?.Key == "java"
        ? $"{Java.WorkspaceName} · {Java.SelectedVersionLabel}"
        : BedrockStatusDetail;
    public string GameStatusGlyph => CurrentNavigation?.Key == "java"
        ? "\uE945"
        : BedrockStatusGlyph;

    public ICommand NavigateCommand { get; }
    public ICommand DismissToastCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand OpenCommandPaletteCommand { get; }

    public bool IsGameRunning => Game.IsGameRunning;
    public bool IsLightTheme => Settings.IsLightTheme;
    public string? BackdropSource => Settings.BackgroundPath;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => SetProperty(ref _isFullscreen, value);
    }
    public UpdateInfo? LastUpdateInfo => _lastUpdateInfo;

    /// <summary>Nombre mostrado de la cuenta de XD Launcher (viene del servidor).</summary>
    public string AccountDisplayName =>
        string.IsNullOrWhiteSpace(Account.DisplayName) ? Account.Email : Account.DisplayName;

    public string AccountEmail => Account.Email;
    public string AccountInitial => Account.Initial;
    public string? AccountAvatarPath => Account.AvatarPath;
    public string AccountProviderLabel => Account.ProviderLabel;
    public string LauncherVersion => AppInfo.FullVersion;
    public string XdAccountStatusText => $"{Account.ProviderLabel} · {AccountEmail}";

    /// <summary>Actualiza la cuenta sin recrear la ventana cuando llega un handoff desde la web.</summary>
    public void UpdateAccount(XdAccount account)
    {
        _account = account;
        Home.UpdateAccount(account);
        SettingsPage.UpdateAccount(account);
        ProfilePage.UpdateAccount(account);
        OnPropertyChanged(nameof(Account));
        OnPropertyChanged(nameof(AccountDisplayName));
        OnPropertyChanged(nameof(AccountEmail));
        OnPropertyChanged(nameof(AccountInitial));
        OnPropertyChanged(nameof(AccountAvatarPath));
        OnPropertyChanged(nameof(AccountProviderLabel));
        OnPropertyChanged(nameof(XdAccountStatusText));
    }

    /// <summary>Solicita el cierre de sesión y el regreso a la pantalla de acceso.</summary>
    public event Action? SignOutRequested;
    public event Action? CommandPaletteRequested;

    public RelayCommand SignOutCommand { get; }

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (SetProperty(ref _isSidebarCollapsed, value))
            {
                OnPropertyChanged(nameof(SidebarWidth));
                OnPropertyChanged(nameof(SidebarToggleGlyph));
            }
        }
    }

    public double SidebarWidth => IsSidebarCollapsed ? 84 : 264;
    public double TitleBarHeight => 42;
    public string SidebarToggleGlyph => IsSidebarCollapsed ? "\uE76C" : "\uE76B";

    /// <summary>Navega a la página con la clave indicada.</summary>
    public void Navigate(string key)
    {
        var target = Navigation.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
        if (target is not null)
            CurrentNavigation = target;
    }

    private void OnNavigated(string key)
    {
        LogService.Info($"Navegación: {key}.");
        OnPropertyChanged(nameof(GameStatusText));
        OnPropertyChanged(nameof(GameStatusDetail));
        OnPropertyChanged(nameof(GameStatusGlyph));
    }

    private void OnToastRequested(ToastMessage toast)
    {
        // ToastHub puede notificar desde hilos secundarios: siempre se vuelve al hilo de UI.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() =>
            {
                Toasts.Add(toast);
                ScheduleToastDismiss(toast);
            });
        }
        else
        {
            Toasts.Add(toast);
            ScheduleToastDismiss(toast);
        }

        while (Toasts.Count > 4)
            Toasts.RemoveAt(0);
    }

    /// <summary>Cierra automáticamente la notificación tras unos segundos.</summary>
    private void ScheduleToastDismiss(ToastMessage toast)
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(toast.IsError ? 9 : 6)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Toasts.Remove(toast);
        };
        timer.Start();
    }

    private void OnGameLaunched(LaunchResult result)
    {
        LogService.Info(result.Success
            ? $"Minecraft lanzado (PID {result.ProcessId})."
            : $"No se pudo lanzar Minecraft: {result.Message}");
    }

    private void OnFullscreenChanged(bool isFullscreen)
    {
        _isFullscreen = isFullscreen;
        OnPropertyChanged(nameof(IsFullscreen));
    }

    private void OnUpdateChecked(UpdateInfo info)
    {
        _lastUpdateInfo = info;
        OnPropertyChanged(nameof(LastUpdateInfo));
    }

    private void OnUpdateAvailable(UpdateInfo info, bool isAvailable)
    {
        _lastUpdateInfo = info;
        OnPropertyChanged(nameof(LastUpdateInfo));

        if (isAvailable && Settings.ShowUpdateNotifications && !string.IsNullOrWhiteSpace(info.LatestVersion))
            ToastHub.Info($"Nueva versión {info.LatestVersion} disponible.");

        if (!isAvailable || !Settings.Settings.AutoCheckUpdates)
            return;

        _ = ApplyGitHubUpdateAsync(info);
    }

    private async Task ApplyGitHubUpdateAsync(UpdateInfo info)
    {
        try
        {
            var updated = await _updateService.UpdateAsync(info, CancellationToken.None);
            if (updated)
                ToastHub.Success($"Launcher actualizado a la versión {info.LatestVersion}.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo actualizar el launcher desde GitHub.");
            ToastHub.Error("No se pudo completar la actualización automática. Usa el instalador manual si hace falta.");
        }
    }

    private void OnStatusMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        LogService.Info(message);
    }

    /// <summary>Inicialización asíncrona de la interfaz: ajustes base y comprobación de actualizaciones.</summary>
    public async Task InitializeAsync()
    {
        if (IsInitializing)
            return;

        IsInitializing = true;
        try
        {
            Settings.ApplyInitial();

            if (Settings.Settings.AutoCheckUpdates)
            {
                InitializationStatus = "Comprobando actualizaciones…";
                await _updateCheckService.CheckForUpdatesAsync();
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo completar la inicialización del launcher.");
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>Libera las suscripciones globales al cerrar el launcher.</summary>
    public void Shutdown()
    {
        ToastHub.ToastRequested -= OnToastRequested;
        Game.Launched -= OnGameLaunched;
        MobileState.FullscreenChanged -= OnFullscreenChanged;
        _updateService.UpdateChecked -= OnUpdateChecked;
        _updateService.UpdateAvailable -= OnUpdateAvailable;
        _updateCheckService.UpdateChecked -= OnUpdateChecked;
        _updateCheckService.UpdateAvailable -= OnUpdateAvailable;
        _updateCheckService.StatusMessage -= OnStatusMessage;
    }
}