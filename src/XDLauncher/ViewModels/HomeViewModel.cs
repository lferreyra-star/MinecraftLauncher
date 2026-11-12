using System.Collections.ObjectModel;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Dashboard that composes game, news and profile state.</summary>
public sealed class HomeViewModel : ObservableObject
{
    private readonly SettingsState _settings;
    private string _selectedEditionId = "bedrock";

    public HomeViewModel(GameState game, CatalogState catalog, ProfileState profile, SettingsState settings,
        MobileConnectState mobile)
    {
        Game = game;
        Catalog = catalog;
        Profile = profile;
        Mobile = mobile;
        _settings = settings;

        OpenNewsCommand = new RelayCommand(parameter =>
        {
            if (parameter is NewsItem { Url: { Length: > 0 } url })
                FileOps.OpenUrl(url);
            else
                FileOps.OpenUrl(AppInfo.StoreWebUrl);
        });
        OpenStoreCommand = new RelayCommand(() => Game.OpenStoreCommand.Execute(null));
        OpenUpdatesCommand = new RelayCommand(() => Game.OpenUpdatesCommand.Execute(null));
        OpenMobileCommand = new RelayCommand(() => OpenMobileRequested?.Invoke());
        SelectEditionCommand = new RelayCommand(parameter =>
        {
            if (parameter is string id)
                SelectedEditionId = id;
        });
        MainActionCommand = new RelayCommand(MainAction);

        Game.PropertyChanged += (_, _) => NotifyAll();
        Catalog.PropertyChanged += (_, _) => NotifyAll();
        Profile.PropertyChanged += (_, _) => NotifyAll();
        settings.AppearanceChanged += () =>
        {
            OnPropertyChanged(nameof(BackgroundPath));
            OnPropertyChanged(nameof(IsLightTheme));
        };
        settings.WindowPlacementChanged += () =>
        {
            OnPropertyChanged(nameof(BackgroundPath));
        };
    }

    public GameState Game { get; }
    public CatalogState Catalog { get; }
    public ProfileState Profile { get; }
    public MobileConnectState Mobile { get; }
    public SettingsState Settings => _settings;
    public JavaViewModel? JavaPage { get; set; }

    public ObservableCollection<NewsItem> News => Catalog.News;
    public bool HasNews => News.Count > 0;
    public bool ShowNews => _settings.Settings.ShowNews;
    public bool IsLightTheme => _settings.Settings.Theme == "Light";
    public string? BackgroundPath => _settings.BackgroundPath;
    public string Greeting => DateTime.Now.Hour switch
    {
        < 6 => "Buenas noches",
        < 12 => "Buenos días",
        < 20 => "Buenas tardes",
        _ => "Buenas noches"
    };

    public string WelcomeName => Profile.DisplayName;
    public string PlayLabel => Game.PlayLabel;
    public string PlayGlyph => Game.PlayGlyph;
    public bool IsInstalled => Game.IsInstalled;
    public bool IsGameRunning => Game.IsGameRunning;
    public string VersionLabel => Game.VersionLabel;
    public string StatusHeadline => Game.StatusHeadline;
    public string StatusDetail => Game.StatusDetail;
    public string UpdateStatus => Game.UpdateStatus;
    public bool HasUpdate => Game.HasUpdate;
    public string NewsStatus => Catalog.StatusMessage;
    public bool IsCatalogOffline => Catalog.IsOffline;
    public AsyncRelayCommand LaunchCommand => Game.LaunchCommand;
    public AsyncRelayCommand VerifyCommand => Game.VerifyCommand;
    public RelayCommand OpenNewsCommand { get; }
    public RelayCommand OpenStoreCommand { get; }
    public RelayCommand OpenUpdatesCommand { get; }
    public RelayCommand OpenMobileCommand { get; }
    public RelayCommand SelectEditionCommand { get; }
    public RelayCommand MainActionCommand { get; }

    /// <summary>Selected edition: java, bedrock or android.</summary>
    public string SelectedEditionId
    {
        get => _selectedEditionId;
        set
        {
            if (value is not ("java" or "bedrock" or "android") ||
                !SetProperty(ref _selectedEditionId, value))
                return;

            OnPropertyChanged(nameof(IsJavaEdition));
            OnPropertyChanged(nameof(IsBedrockEdition));
            OnPropertyChanged(nameof(IsAndroidEdition));
            OnPropertyChanged(nameof(EditionBadgeLabel));
            OnPropertyChanged(nameof(EditionDescription));
            OnPropertyChanged(nameof(HeroHeadline));
            OnPropertyChanged(nameof(HeroDetail));
            OnPropertyChanged(nameof(MainActionLabel));
            OnPropertyChanged(nameof(MainActionGlyph));
            OnPropertyChanged(nameof(ShowVerify));
        }
    }

    public bool IsJavaEdition => SelectedEditionId == "java";
    public bool IsBedrockEdition => SelectedEditionId == "bedrock";
    public bool IsAndroidEdition => SelectedEditionId == "android";
    public bool ShowVerify => IsBedrockEdition && IsInstalled;

    public string EditionBadgeLabel => SelectedEditionId switch
    {
        "java" => "MINECRAFT JAVA EDITION",
        "android" => "MINECRAFT MOBILE CONNECT",
        _ => "MINECRAFT BEDROCK EDITION"
    };

    public string EditionDescription => SelectedEditionId switch
    {
        "java" => "Versiones oficiales de Java con tu perfil local o cuenta Microsoft.",
        "android" => "Transmite Minecraft Bedrock desde tu teléfono Android.",
        _ => "El Minecraft Bedrock instalado en Windows desde Microsoft Store."
    };

    public string HeroHeadline => SelectedEditionId switch
    {
        "java" => "Minecraft Java Edition",
        "android" => "Minecraft en tu móvil",
        _ => Game.StatusHeadline
    };

    public string HeroDetail => SelectedEditionId switch
    {
        "java" => "Instala la versión que quieras, ajusta la memoria y pulsa Jugar para abrir el cliente real de Mojang.",
        "android" => "Conecta tu teléfono con USB o WiFi y juega desde el PC con controles y pantalla completa.",
        _ => Game.StatusDetail
    };

    public string MainActionLabel => SelectedEditionId switch
    {
        "java" => "JUGAR JAVA",
        "android" => "JUGAR EN EL MÓVIL",
        _ => IsInstalled ? Game.PlayLabel : "INSTALAR DESDE STORE"
    };

    public string MainActionGlyph => SelectedEditionId switch
    {
        "java" => "\uE768",
        "android" => "\uE12C",
        _ => IsInstalled ? Game.PlayGlyph : "\uE719"
    };

    private void MainAction()
    {
        switch (SelectedEditionId)
        {
            case "java":
                if (JavaPage?.SelectedVersion is { IsInstalled: true, IsBusy: false } version &&
                    !JavaPage.IsBusy)
                {
                    JavaPage.PlayCommand.Execute(version);
                    return;
                }
                OpenJavaRequested?.Invoke();
                break;
            case "android":
                OpenMobileRequested?.Invoke();
                Mobile.PlayCommand.Execute(null);
                break;
            default:
                if (IsInstalled)
                    LaunchCommand.Execute(null);
                else
                    OpenStoreCommand.Execute(null);
                break;
        }
    }

    public string StatsLabel => Game.IsInstalled
        ? $"Versión {Game.VersionLabel} · {Game.ChannelLabel}"
        : "Sin instalación detectada";

    public string NewsSectionTitle => "Novedades oficiales";
    public string NewsSectionSubtitle => "Información del catálogo de Microsoft Store y canales oficiales.";

    /// <summary>Raised when the user wants to open the MINECRAFT MOBILE CONNECT page.</summary>
    public event Action? OpenMobileRequested;

    /// <summary>Raised when the user wants to open the Minecraft Java page.</summary>
    public event Action? OpenJavaRequested;

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(News));
        OnPropertyChanged(nameof(HasNews));
        OnPropertyChanged(nameof(WelcomeName));
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayGlyph));
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(IsGameRunning));
        OnPropertyChanged(nameof(VersionLabel));
        OnPropertyChanged(nameof(StatusHeadline));
        OnPropertyChanged(nameof(StatusDetail));
        OnPropertyChanged(nameof(UpdateStatus));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(NewsStatus));
        OnPropertyChanged(nameof(IsCatalogOffline));
        OnPropertyChanged(nameof(StatsLabel));
        OnPropertyChanged(nameof(HeroHeadline));
        OnPropertyChanged(nameof(MainActionLabel));
        OnPropertyChanged(nameof(MainActionGlyph));
        OnPropertyChanged(nameof(ShowVerify));
    }
}
