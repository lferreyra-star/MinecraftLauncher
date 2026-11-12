using System.Collections.ObjectModel;
using System.Windows;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly DialogService _dialogs;
    private readonly UpdateService _updateService;
    private readonly UpdateCheckService _updateCheckService;
    private NavigationItem _currentNavigation;
    private bool _isInitializing;
    private bool _isFullscreen;
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
        UpdateCheckService updateCheckService)
    {
        _dialogs = dialogs;
        _updateService = updateService;
        _updateCheckService = updateCheckService;

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
        Home = new HomeViewModel(Game, Catalog, Profile, Settings, MobileState);
        Versions = new VersionsViewModel(Game, Catalog);
        Addons = new AddonsViewModel(addonService, Game, Settings, dialogs);
        Downloads = new DownloadViewModel(downloadService, dialogs);
        Java = new JavaViewModel(Settings, Profile, dialogs);
        Home.JavaPage = Java;
        SettingsPage = new SettingsViewModel(Settings, Profile);


        Navigation =
        [
            new NavigationItem
            {
                Key = "home", Title = "Inicio", Glyph = "\uE80F",
                Subtitle = "Tu centro de mando", Page = Home
            },
            new NavigationItem
            {
                Key = "java", Title = "Minecraft Java", Glyph = "\uE7FC",
                Subtitle = "Versiones, Java y lanzamiento", Page = Java
            },
            new NavigationItem
            {
                Key = "game", Title = "Juego", Glyph = "\uE7FC",
                Subtitle = "Instalación, componentes y estado", Page = Game
            },
            new NavigationItem
            {
                Key = "versions", Title = "Versiones", Glyph = "\uE81C",
                Subtitle = "Actualizaciones oficiales y notas", Page = Versions
            },
            new NavigationItem
            {
                Key = "addons", Title = "Complementos", Glyph = "\uE7B8",
                Subtitle = "Addons, texturas, skins y mapas", Page = Addons
            },
            new NavigationItem
            {
                Key = "downloads", Title = "Descargas", Glyph = "\uE893",
                Subtitle = "Descarga Minecraft y herramientas", Page = Downloads
            },
            new NavigationItem
            {
                Key = "settings", Title = "Ajustes", Glyph = "\uE713",
                Subtitle = "Personalización, cuenta y sistema", Page = SettingsPage
            },
            new NavigationItem
            {
                Key = "mobile", Title = "Móvil", Glyph = "\uE12C",
                Subtitle = "Conecta tu teléfono Android", Page = MobileState
            }
        ];

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

        ToastHub.ToastRequested += OnToastRequested;
        Game.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(GameStatusText));
            OnPropertyChanged(nameof(GameStatusDetail));
            OnPropertyChanged(nameof(IsGameRunning));
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
    public HomeViewModel Home { get; }
    public VersionsViewModel Versions { get; }
    public AddonsViewModel Addons { get; }
    public DownloadViewModel Downloads { get; }
    public JavaViewModel Java { get; }
    public SettingsViewModel SettingsPage { get; }
    public MobileConnectState MobileState { get; }
    public IReadOnlyList<NavigationItem> Navigation { get; }
    public ObservableCollection<ToastMessage> Toasts { get; }

    public NavigationItem CurrentNavigation
    {
        get => _currentNavigation;
        set
        {
            if (value is null || SetProperty(ref _currentNavigation, value))
            {
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(CurrentTitle));
                OnPropertyChanged(nameof(CurrentSubtitle));
                OnPropertyChanged(nameof(CurrentGlyph));
                if (value is not null)
                    OnNavigated(value.Key);
            }
        }
    }

    public object CurrentPage => CurrentNavigation.Page;
    public string CurrentTitle => CurrentNavigation.Title;
    public string CurrentSubtitle => CurrentNavigation.Subtitle;
    public string CurrentGlyph => CurrentNavigation.Glyph;

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
    public string AccountStatusText => Profile.IsMicrosoftAccount
        ? $"Xbox Live · {Profile.DisplayName}"
        : "Perfil local";
    public string GameStatusText => Game.IsInstalled
        ? Game.IsGameRunning ? "Minecraft en ejecución" : $"Listo · {Game.VersionLabel}"
        : "Minecraft no instalado";
    public string GameStatusDetail => Game.IsInstalled
        ? $"{Game.DisplayName} · {Game.ChannelLabel}"
        : "Instálalo desde Microsoft Store";
    public string GameStatusGlyph => Game.IsInstalled
     
