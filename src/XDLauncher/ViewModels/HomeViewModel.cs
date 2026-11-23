using System.Collections.ObjectModel;
using System.Windows.Threading;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Dashboard that composes game, news and profile state.</summary>
public sealed class HomeViewModel : ObservableObject
{
    private readonly SettingsState _settings;
    private XdAccount _account;
    private string _selectedEditionId = "bedrock";


    private readonly DispatcherTimer _bannerTimer;
    private int _bannerIndex;

    public IReadOnlyList<string> BannerTitles => Settings.Backgrounds.Select(b => b.Name).ToList();

    public int BannerIndex
    {
        get => _bannerIndex;
        set
        {
            var list = BannerTitles;
            if (list.Count == 0) return;
            var next = (value + list.Count) % list.Count;
            if (!SetProperty(ref _bannerIndex, next)) return;
            OnPropertyChanged(nameof(CurrentBannerPath));
            OnPropertyChanged(nameof(BannerTitle));
            OnPropertyChanged(nameof(BannerDescription));
            OnPropertyChanged(nameof(BannerPosition));
        }
    }

    public string? CurrentBannerPath
    {
        get
        {
            var list = Settings.Backgrounds;
            return list.Count == 0 ? null : list[_bannerIndex % list.Count].Path;
        }
    }

    public string BannerTitle => BannerTitles.Count == 0 ? "XD Launcher Eclipse" : BannerTitles[_bannerIndex % BannerTitles.Count];

    public string BannerDescription => BannerTitle switch
    {
        "Bosque cinematográfico" => "Explora un bosque con luz volumétrica y profundidad real.",
        "Aurora boreal" => "Cielos animados con auroras sobre montañas lejanas.",
        "Amanecer en las montañas" => "El primer luz del día sobre cumbres infinitas.",
        "Cueva de cristal" => "Cristales luminosos en las profundidades del mundo.",
        "Nether cinematográfico" => "Un portal ardiente hacia una dimensión hostil.",
        "El End" => "Islas flotantes y el vacío púrpura de otra dimensión.",
        "Bioma de cerezo" => "Pétalos rosados sobre un paisaje sereno.",
        "Montañas nevadas" => "Cumbres blancas bajo un cielo glacial.",
        "Ciudad nocturna" => "Ventanas encendidas en una metrópolis interminable.",
        "Bosque nocturno" => "Siluetas oscuras y estrellas sobre el dosel.",
        "Bosque mágico" => "Luces suaves entre árboles ancestrales.",
        "Mundos fantásticos" => "Islas suspendidas y cascadas imposibles.",
        _ => "Paisajes cinematográficos para tu aventura."
    };

    public string BannerPosition => $"{_bannerIndex + 1} / {Math.Max(BannerTitles.Count, 1)}";

    public HomeViewModel(GameState game, CatalogState catalog, ProfileState profile, SettingsState settings,
        MobileConnectState mobile, XdAccount account)
    {
        Game = game;
        Catalog = catalog;
        Profile = profile;
        Mobile = mobile;
        _account = account;
        _settings = settings;
        var savedIndex = settings.Backgrounds.ToList().FindIndex(b => b.Id == settings.Settings.BackgroundId);
        _bannerIndex = savedIndex >= 0 ? savedIndex : 0;
        _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _bannerTimer.Tick += (_, _) => NextBanner();
        _bannerTimer.Start();

        NextBannerCommand = new RelayCommand(NextBanner);
        PreviousBannerCommand = new RelayCommand(PreviousBanner);
        SelectBannerCommand = new RelayCommand<string>(name =>
        {
            var index = BannerTitles.ToList().IndexOf(name);
            if (index >= 0) BannerIndex = index;
        });

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
    public XdAccount Account => _account;
    public SettingsState Settings => _settings;

    /// <summary>Actualiza la cuenta mostrada cuando el handoff web cambia de usuario.</summary>
    public void UpdateAccount(XdAccount account)
    {
        _account = account;
        OnPropertyChanged(nameof(Account));
        OnPropertyChanged(nameof(WelcomeName));
        OnPropertyChanged(nameof(AccountDisplayName));
        OnPropertyChanged(nameof(AccountEmail));
        OnPropertyChanged(nameof(AccountInitial));
        OnPropertyChanged(nameof(AccountAvatarPath));
        OnPropertyChanged(nameof(AccountProviderLabel));
        OnPropertyChanged(nameof(AccountStatusLabel));
    }
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

    public string WelcomeName => Account.DisplayName ?? Account.Email;
    public string AccountDisplayName => Account.DisplayName ?? Account.Email;
    public string AccountEmail => Account.Email;
    public string AccountInitial => Account.Initial;
    public string? AccountAvatarPath => Account.AvatarPath;
    public string AccountProviderLabel => Account.ProviderLabel;
    public string AccountStatusLabel => $"{Account.ProviderLabel} · {AccountEmail}";
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
    public RelayCommand NextBannerCommand { get; }
    public RelayCommand PreviousBannerCommand { get; }
    public RelayCommand<string> SelectBannerCommand { get; }
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
            OnPropertyChanged(nameof(MainActionHint));
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

    public string MainActionHint => SelectedEditionId switch
    {
        "java" => JavaPage?.SelectedVersion is { IsInstalled: true, IsBusy: false } && !JavaPage.IsBusy
            ? "Abre Minecraft Java con la versión seleccionada."
            : "Elige una versión instalada en Java Edition para habilitar JUGAR.",
        "android" => "Conecta el teléfono por USB o WiFi para jugar en streaming.",
        _ => IsInstalled ? "Abre Minecraft Bedrock." : "Abre Microsoft Store para instalar Minecraft Bedrock."
    };

    public string MainActionGlyph => SelectedEditionId switch
    {
        "java" => "\uE768",
        "android" => "\uE12C",
        _ => IsInstalled ? Game.PlayGlyph : "\uE719"
    };

    private void NextBanner() => BannerIndex = _bannerIndex + 1;

    private void PreviousBanner() => BannerIndex = _bannerIndex - 1;

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
        OnPropertyChanged(nameof(MainActionHint));
        OnPropertyChanged(nameof(ShowVerify));
    }
}
