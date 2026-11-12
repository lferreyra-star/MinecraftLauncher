using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>
/// Administrador de versiones de Minecraft Java con dos workspaces independientes
/// (Premium y No-Premium), perfiles por versión, Fabric real y lanzamiento del cliente.
/// </summary>
public sealed class JavaViewModel : ObservableObject, IDisposable
{
    private readonly JavaService _premiumJava;
    private readonly JavaService _noPremiumJava;
    private readonly JavaProfileStore _premiumStore;
    private readonly JavaProfileStore _noPremiumStore;
    private readonly SettingsState _settings;
    private readonly ProfileState _profile;
    private readonly DialogService _dialogs;
    private readonly List<JavaVersionEntry> _all = [];

    private JavaWorkspaceKind _activeWorkspace = JavaWorkspaceKind.NoPremium;
    private JavaVersionEntry? _selectedVersion;
    private string _searchText = string.Empty;
    private string _selectedFilter = "Todas";
    private bool _isLoading;
    private bool _isBusy;
    private bool _isDetectingJava;
    private bool _isProgressIndeterminate;
    private double _progressPercent;
    private string _progressStage = "Listo";
    private string _progressDetail = string.Empty;
    private string _statusMessage = "Cargando el manifiesto oficial de Mojang…";
    private string _javaInfo = "Java aún no detectado";
    private string _latestReleaseLabel = "—";
    private string _premiumStatus = string.Empty;
    private CancellationTokenSource? _operationCts;
    private bool _disposed;

    public JavaViewModel(SettingsState settings, ProfileState profile, DialogService dialogs)
    {
        _settings = settings;
        _profile = profile;
        _dialogs = dialogs;

        _premiumJava = new JavaService(Paths.CreatePremiumJavaWorkspace());
        _noPremiumJava = new JavaService(Paths.CreateNoPremiumJavaWorkspace());
        _premiumStore = new JavaProfileStore(_premiumJava.Workspace);
        _noPremiumStore = new JavaProfileStore(_noPremiumJava.Workspace);

        InitializeWorkspaceDefaults(_premiumStore,
            _profile.IsMicrosoftAccount ? _profile.DisplayName : _settings.Settings.LocalProfileName);
        InitializeWorkspaceDefaults(_noPremiumStore,
            string.IsNullOrWhiteSpace(_settings.Settings.LocalProfileName)
                ? _settings.Settings.Java.PlayerName
                : _settings.Settings.LocalProfileName);

        _activeWorkspace = _profile.IsMicrosoftAccount && _settings.IsMicrosoftConfigured
            ? JavaWorkspaceKind.Premium
            : JavaWorkspaceKind.NoPremium;

        Filters = ["Todas", "Release", "Snapshot", "Antiguas", "Instaladas"];
        ModLoaderOptions = [JavaModLoaders.Vanilla, JavaModLoaders.Fabric];

        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(force: true),
            () => !IsBusy && !IsLoading, ShowError);
        InstallCommand = new AsyncRelayCommand<JavaVersionEntry>(InstallAsync,
            entry => entry is { IsInstalled: false, IsBusy: false } && !IsBusy, ShowError);
        PlayCommand = new AsyncRelayCommand<JavaVersionEntry>(PlayAsync,
            entry => entry is { IsInstalled: true, IsBusy: false } && !IsBusy && !IsVersionRunning(entry.Id),
            ShowError);
        ConfigureCommand = new RelayCommand<JavaVersionEntry>(ConfigureVersion, entry => entry is not null);
        DeleteCommand = new AsyncRelayCommand<JavaVersionEntry>(DeleteAsync,
            entry => entry is { IsInstalled: true, IsBusy: false } && !IsBusy && !IsVersionRunning(entry.Id),
            ShowError);
        SelectCommand = new RelayCommand<JavaVersionEntry>(entry =>
        {
            if (entry is not null)
                SelectedVersion = entry;
        });
        PrimaryCommand = new AsyncRelayCommand(PrimaryAsync,
            () => SelectedVersion is not null && !IsBusy && !IsSelectedRunning, ShowError);
        CancelCommand = new RelayCommand(CancelOperation, () => IsBusy && _operationCts is not null);
        CloseGameCommand = new RelayCommand(CloseGame, () => IsSelectedRunning);
        InstallFabricCommand = new AsyncRelayCommand(InstallSelectedFabricAsync,
            () => SelectedVersion is { IsInstalled: true } && ActiveProfile?.ModLoader == JavaModLoaders.Fabric &&
                  !IsSelectedFabricReady && !IsBusy, ShowError);
        DetectJavaCommand = new AsyncRelayCommand(DetectJavaAsync, () => !IsDetectingJava && !IsBusy);
        SelectWorkspaceCommand = new RelayCommand(parameter =>
        {
            if (parameter is string value && Enum.TryParse<JavaWorkspaceKind>(value, out var kind))
                SetActiveWorkspace(kind);
        });
        BrowseJavaCommand = new RelayCommand(BrowseJava);
        ResetJavaPathCommand = new RelayCommand(() => JavaPath = string.Empty);
        BrowseGameFolderCommand = new RelayCommand(BrowseGameFolder);
        ResetGameFolderCommand = new RelayCommand(() => GameDirectory = string.Empty);
        OpenWorkspaceFolderCommand = new RelayCommand(() => FileOps.OpenFolder(ActiveWorkspace.Root));
        OpenProfileModsCommand = new RelayCommand(OpenProfileMods);
        OpenProfileSavesCommand = new RelayCommand(OpenProfileSaves);
        OpenWorkspaceLogsCommand = new RelayCommand(() => FileOps.OpenFolder(ActiveWorkspace.LogsDirectory));
        ModDownloadCommand = new AsyncRelayCommand(ModDownloadAsync, () => SelectedVersion is { IsInstalled: true } && !IsBusy && !IsVersionRunning(SelectedVersion.Id));

        _premiumJava.RunningChanged += OnRunningChanged;
        _noPremiumJava.RunningChanged += OnRunningChanged;
        _profile.PropertyChanged += OnProfileChanged;
        UpdatePremiumStatus();
    }

    public ObservableCollection<JavaVersionEntry> Versions { get; } = [];
    public IReadOnlyList<string> Filters { get; }
    public IReadOnlyList<string> ModLoaderOptions { get; }

    public JavaService ActiveService => ActiveWorkspace.IsPremium ? _premiumJava : _noPremiumJava;
    public JavaProfileStore ActiveStore => ActiveWorkspace.IsPremium ? _premiumStore : _noPremiumStore;
    public JavaWorkspaceKind ActiveWorkspaceKind => _activeWorkspace;
    public JavaWorkspace ActiveWorkspace => ActiveService.Workspace;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand<JavaVersionEntry> InstallCommand { get; }
    public AsyncRelayCommand<JavaVersionEntry> PlayCommand { get; }
    public RelayCommand<JavaVersionEntry> ConfigureCommand { get; }
    public AsyncRelayCommand<JavaVersionEntry> DeleteCommand { get; }
    public RelayCommand<JavaVersionEntry> SelectCommand { get; }
    public AsyncRelayCommand PrimaryCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand CloseGameCommand { get; }
    public AsyncRelayCommand InstallFabricCommand { get; }
    public AsyncRelayCommand DetectJavaCommand { get; }
    public RelayCommand SelectWorkspaceCommand { get; }
    public RelayCommand BrowseJavaCommand { get; }
    public RelayCommand ResetJavaPathCommand { get; }
    public RelayCommand BrowseGameFolderCommand { get; }
    public RelayCommand ResetGameFolderCommand { get; }
    public RelayCommand OpenWorkspaceFolderCommand { get; }
    public RelayCommand OpenProfileModsCommand { get; }
    public RelayCommand OpenProfileSavesCommand { get; }
    public RelayCommand OpenWorkspaceLogsCommand { get; }
    public AsyncRelayCommand ModDownloadCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
                RefreshView();
        }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !SetProperty(ref _selectedFilter, value))
                return;
            RefreshView();
        }
    }

    public JavaVersionEntry? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (!SetProperty(ref _selectedVersion, value))
                return;
            NotifySelection();
            RaiseCommandStates();
            OnPropertyChanged(nameof(IsSelectedRunning));
        }
    }

    public JavaProfile? ActiveProfile => SelectedVersion is null
        ? null
        : ActiveStore.GetOrCreate(SelectedVersion.Id);

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                RaiseCommandStates();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;
            OnPropertyChanged(nameof(ShowProgress));
            RaiseCommandStates();
        }
    }

    public bool IsDetectingJava
    {
        get => _isDetectingJava;
        private set => SetProperty(ref _isDetectingJava, value);
    }

    public bool ShowProgress => IsBusy;

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string ProgressStage
    {
        get => _progressStage;
        private set => SetProperty(ref _progressStage, value);
    }

    public string ProgressDetail
    {
        get => _progressDetail;
        private set => SetProperty(ref _progressDetail, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string JavaInfo
    {
        get => _javaInfo;
        private set => SetProperty(ref _javaInfo, value);
    }

    public string LatestReleaseLabel
    {
        get => _latestReleaseLabel;
        private set => SetProperty(ref _latestReleaseLabel, value);
    }

    public string PremiumStatus
    {
        get => _premiumStatus;
        private set => SetProperty(ref _premiumStatus, value);
    }

    public bool HasVersions => Versions.Count > 0;
    public string VisibleCountLabel => $"{Versions.Count} de {_all.Count} versiones";
    public string InstalledCountLabel => $"{_all.Count(version => version.IsInstalled)} instaladas";

    public string PremiumInstalledCountLabel => $"{_premiumJava.GetInstalledVersionIds().Count} instaladas";
    public string NoPremiumInstalledCountLabel => $"{_noPremiumJava.GetInstalledVersionIds().Count} instaladas";

    public bool IsPremiumTab => _activeWorkspace == JavaWorkspaceKind.Premium;
    public bool IsNoPremiumTab => _activeWorkspace == JavaWorkspaceKind.NoPremium;
    public string WorkspaceName => ActiveWorkspace.DisplayName;
    public string WorkspacePath => ActiveWorkspace.Root;
    public string WorkspaceSummary => $"{WorkspaceName} · {WorkspacePath}";

    public bool HasSelection => SelectedVersion is not null;
    public bool IsSelectedInstalled => SelectedVersion?.IsInstalled == true;
    public bool IsSelectedRunning => SelectedVersion is not null && IsVersionRunning(SelectedVersion.Id);
    public bool HasPremiumAccount => _profile.IsMicrosoftAccount && _settings.IsMicrosoftConfigured;
    public bool AccountWarningVisible => ActiveWorkspace.IsPremium && !HasPremiumAccount;
    public bool IsPlayerNameEditable => !ActiveWorkspace.IsPremium;

    public string SelectedVersionLabel => SelectedVersion?.Id ?? "Ninguna versión";
    public string SelectedVersionType => SelectedVersion?.TypeLabel ?? string.Empty;
    public string SelectedVersionDate => SelectedVersion is null
        ? string.Empty
        : $"Publicada el {SelectedVersion.DateLabel}";
    public string SelectedProfileSummary => ActiveProfile is null
        ? "Sin perfil"
        : $"{ActiveProfile.ModLoader} · {ActiveWorkspace.DisplayName}" +
          (ActiveWorkspace.IsPremium ? string.Empty : $" · {ActiveProfile.PlayerName}");

    public string SelectedFabricVersionId => SelectedVersion is null
        ? string.Empty
        : JavaService.FabricVersionId(SelectedVersion.Id);

    public bool IsSelectedFabricReady => SelectedVersion is not null &&
        ActiveService.IsInstalled(JavaService.FabricVersionId(SelectedVersion.Id));

    public bool IsSelectedFabricPending => SelectedVersion is { IsInstalled: true } &&
        ActiveProfile?.ModLoader == JavaModLoaders.Fabric && !IsSelectedFabricReady;

    public string PrimaryActionLabel
    {
        get
        {
            if (SelectedVersion is null)
                return "Selecciona una versión";
            if (IsSelectedRunning)
                return "EN EJECUCIÓN";
            if (!SelectedVersion.IsInstalled)
                return "INSTALAR VERSIÓN";
            if (IsSelectedFabricPending)
                return "INSTALAR FABRIC Y JUGAR";
            return "JUGAR MINECRAFT JAVA";
        }
    }

    public int MemoryMb
    {
        get => ActiveProfile?.MemoryMb ?? ActiveStore.Config.DefaultMemoryMb;
        set
        {
            var profile = ActiveProfile;
            if (profile is null)
                return;

            var normalized = Math.Clamp(value, 1024, 32768);
            if (profile.MemoryMb == normalized)
                return;
            profile.MemoryMb = normalized;
            ActiveStore.SaveProfile(profile);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MemoryLabel));
            NotifySelection();
        }
    }

    public string MemoryLabel => $"{MemoryMb} MB · {MemoryMb / 1024d:0.#} GB";

    public string PlayerName
    {
        get => ActiveProfile?.PlayerName ?? ActiveStore.Config.DefaultPlayerName;
        set
        {
            var profile = ActiveProfile;
            if (profile is null)
                return;

            var name = string.IsNullOrWhiteSpace(value) ? "Jugador" : value.Trim();
            if (name.Length > 16)
                name = name[..16];
            if (profile.PlayerName == name)
                return;
            profile.PlayerName = name;
            ActiveStore.SaveProfile(profile);
            OnPropertyChanged();
            ApplyEntryMetadata(SelectedVersion!);
            NotifySelection();
        }
    }

    public string JavaPath
    {
        get => ActiveProfile?.JavaPath ?? ActiveStore.Config.DefaultJavaPath;
        set
        {
            var profile = ActiveProfile;
            var path = value?.Trim() ?? string.Empty;
            if (profile is null || profile.JavaPath == path)
                return;
            profile.JavaPath = path;
            ActiveStore.SaveProfile(profile);
            OnPropertyChanged();
            OnPropertyChanged(nameof(JavaPathHint));
            OnPropertyChanged(nameof(IsJavaPathCustom));
        }
    }

    public bool IsJavaPathCustom => !string.IsNullOrWhiteSpace(JavaPath);
    public string JavaPathHint => IsJavaPathCustom
        ? JavaPath
        : "Automático: se detecta un Java 64 bits o se descarga el runtime oficial de Mojang.";

    public string GameDirectory
    {
        get => ActiveProfile?.GameDirectory ?? string.Empty;
        set
        {
            var profile = ActiveProfile;
            if (profile is null)
                return;

            var path = value?.Trim() ?? string.Empty;
            if (profile.GameDirectory == path)
                return;
            profile.GameDirectory = path;
            ActiveStore.SaveProfile(profile);
            OnPropertyChanged();
            OnPropertyChanged(nameof(GameDirectoryHint));
            OnPropertyChanged(nameof(IsGameDirectoryCustom));
            OnPropertyChanged(nameof(ResolvedGameDirectory));
        }
    }

    public bool IsGameDirectoryCustom => !string.IsNullOrWhiteSpace(GameDirectory);
    public string ResolvedGameDirectory => ActiveProfile is null
        ? ActiveWorkspace.DefaultGameDirectory
        : ActiveWorkspace.GetGameDirectory(ActiveProfile);
    public string GameDirectoryHint => IsGameDirectoryCustom
        ? GameDirectory
        : ResolvedGameDirectory;

    public string ModLoader
    {
        get => ActiveProfile?.ModLoader ?? JavaModLoaders.Vanilla;
        set
        {
            var profile = ActiveProfile;
            if (profile is null || value is not (JavaModLoaders.Vanilla or JavaModLoaders.Fabric))
                return;
            if (profile.ModLoader == value)
                return;
            profile.ModLoader = value;
            if (value == JavaModLoaders.Vanilla)
                profile.FabricLoaderVersion = string.Empty;
            ActiveStore.SaveProfile(profile);
            ApplyEntryMetadata(SelectedVersion!);
            OnPropertyChanged();
            OnPropertyChanged(nameof(FabricLoaderVersion));
            OnPropertyChanged(nameof(FabricLoaderLabel));
            OnPropertyChanged(nameof(IsSelectedFabricPending));
            OnPropertyChanged(nameof(IsSelectedFabricReady));
            OnPropertyChanged(nameof(SelectedProfileSummary));
            NotifySelection();
            RaiseCommandStates();
        }
    }

    public string FabricLoaderVersion => ActiveProfile?.FabricLoaderVersion ?? string.Empty;
    public string FabricLoaderLabel => string.IsNullOrWhiteSpace(FabricLoaderVersion)
        ? "No instalado"
        : FabricLoaderVersion;

    public async Task EnsureLoadedAsync()
    {
        if (_all.Count > 0)
        {
            RefreshInstalledState();
            return;
        }

        await RefreshAsync();
    }

    public async Task RefreshAsync(bool force = true)
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusMessage = "Consultando el manifiesto oficial de Mojang…";
        try
        {
            var manifest = await ActiveService.GetManifestAsync(force);
            _all.Clear();
            _all.AddRange(manifest.Versions);
            LatestReleaseLabel = manifest.LatestRelease ?? "—";

            RefreshInstalledState();

            if (SelectedVersion is null || !_all.Contains(SelectedVersion))
            {
                SelectedVersion =
                    _all.FirstOrDefault(version => version.Id == ActiveStore.Config.LastVersionId) ??
                    _all.FirstOrDefault(version => version.Id == manifest.LatestRelease) ??
                    _all.FirstOrDefault();
            }

            RefreshView();
            StatusMessage = $"Manifiesto oficial actualizado · {_all.Count} versiones disponibles.";
            LogService.Info($"Manifiesto Java cargado ({WorkspaceName}): {_all.Count} versiones.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo cargar el manifiesto de Minecraft Java.");
            StatusMessage = "No se pudo consultar el manifiesto oficial. Revisa tu conexión.";
            ToastHub.Error("No se pudo cargar la lista de versiones Java.");
        }
        finally
        {
            IsLoading = false;
            NotifyCounts();
        }
    }

    public async Task DetectJavaAsync()
    {
        if (IsDetectingJava)
            return;

        IsDetectingJava = true;
        JavaInfo = "Detectando Java instalado…";
        try
        {
            var versionId = SelectedVersion is null
                ? ActiveStore.Config.LastVersionId
                : GetLaunchVersionId(SelectedVersion);
            var required = ActiveService.GetRequiredJavaMajor(versionId);
            var preferred = ActiveProfile?.JavaPath;
            var info = await ActiveService.DetectJavaAsync(required, NullIfEmpty(preferred ?? string.Empty));
            JavaInfo = info is null
                ? $"No se encontró Java {required} de 64 bits. Al jugar se descargará automáticamente el runtime oficial."
                : $"Java {info.MajorVersion} ({info.VersionText}) · {info.Source} · {info.Path}";
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo detectar Java: " + ex.Message);
            JavaInfo = "No se pudo detectar Java. Se intentará descargar el runtime oficial al jugar.";
        }
        finally
        {
            IsDetectingJava = false;
            DetectJavaCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task ModDownloadAsync()
    {
        if (SelectedVersion is null || SelectedVersion.IsBusy)
            return;

        var profile = ActiveStore.GetOrCreate(SelectedVersion.Id);
        _operationCts = new CancellationTokenSource();
        BeginOperation("Descargando mods para " + SelectedVersion.Id, SelectedVersion.Id);
        StatusMessage = $"Iniciando descarga de mods para {SelectedVersion.Id}…";
        var progress = CreateProgress();

        try
        {
            // Download compatible mods for this version
            var modDownloadService = new ModDownloadService();
            await modDownloadService.DownloadModsAsync(SelectedVersion.Id, ActiveWorkspace, profile, progress, _operationCts.Token);

            profile.LastPlayedAt = DateTimeOffset.Now;
            ActiveStore.SaveProfile(profile);
            ActiveStore.Config.LastVersionId = SelectedVersion.Id;
            ActiveStore.SaveConfig();

            StatusMessage = $"Mods descargados para {SelectedVersion.Id}.";
            ToastHub.Success($"Mods descargados para {SelectedVersion.Id}.");
            RefreshView();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Descarga de mods cancelada.";
            ToastHub.Warning("Descarga de mods cancelada.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudieron descargar los mods para " + SelectedVersion.Id);
            StatusMessage = "No se pudieron descargar los mods.";
            _dialogs.ShowError("No se pudieron descargar los mods",
                $"No se pudieron descargar los mods para {SelectedVersion.Id}.{Environment.NewLine}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            EndOperation();
        }
    }

    private void SetActiveWorkspace(JavaWorkspaceKind kind)
    {
        if (kind == _activeWorkspace)
            return;

        _activeWorkspace = kind;
        OnPropertyChanged(nameof(ActiveWorkspaceKind));
        OnPropertyChanged(nameof(IsPremiumTab));
        OnPropertyChanged(nameof(IsNoPremiumTab));
        OnPropertyChanged(nameof(WorkspaceName));
        OnPropertyChanged(nameof(WorkspacePath));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(IsPlayerNameEditable));
        ApplyAllEntryMetadata();
        RefreshView();
        NotifyCounts();
        NotifySelection();
        UpdatePremiumStatus();
        RaiseCommandStates();
        StatusMessage = $"Workspace {WorkspaceName} activo · {WorkspacePath}";
        LogService.Info("Workspace Java activo: " + WorkspacePath);
    }

    private async Task PrimaryAsync()
    {
        var version = SelectedVersion;
        if (version is null)
            return;

        if (!version.IsInstalled)
            await InstallAsync(version);
        else
            await PlayAsync(version);
    }

    private async Task InstallAsync(JavaVersionEntry? entry)
    {
        if (entry is null || entry.IsBusy)
            return;

        var profile = ActiveStore.GetOrCreate(entry.Id);
        entry.IsBusy = true;
        _operationCts = new CancellationTokenSource();
        BeginOperation("Preparando la instalación…", entry.Id);
        StatusMessage = $"Instalando Minecraft {entry.Id} en {WorkspaceName}…";
        var progress = CreateProgress();

        try
        {
            await ActiveService.InstallAsync(entry.Id, progress, _operationCts.Token);

            if (profile.ModLoader == JavaModLoaders.Fabric)
            {
                try
                {
                    var loader = await ActiveService.InstallFabricAsync(entry.Id, progress, _operationCts.Token);
                    profile.FabricLoaderVersion = loader ?? string.Empty;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogService.Warn("No se pudo instalar Fabric: " + ex.Message);
                    profile.ModLoader = JavaModLoaders.Vanilla;
                    profile.FabricLoaderVersion = string.Empty;
                    ToastHub.Warning("La versión vanilla se instaló, pero Fabric no pudo instalarse. Se configuró Vanilla.");
                }
            }

            ActiveStore.SaveProfile(profile);
            entry.IsInstalled = ActiveService.IsInstalled(entry.Id);
            ActiveStore.Config.LastVersionId = entry.Id;
            ActiveStore.SaveConfig();
            SelectedVersion = entry;
            ApplyEntryMetadata(entry);
            RefreshView();

            StatusMessage = entry.IsInstalled
                ? $"Minecraft {entry.Id} instalado en {WorkspaceName}."
                : $"La instalación de {entry.Id} terminó con archivos incompletos.";
            if (entry.IsInstalled)
                ToastHub.Success($"Minecraft {entry.Id} instalado.");
            else
                ToastHub.Warning("La instalación quedó incompleta.");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Instalación cancelada.";
            ToastHub.Warning("Instalación cancelada.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo instalar la versión Java " + entry.Id);
            StatusMessage = "La instalación falló.";
            _dialogs.ShowError("No se pudo instalar Minecraft Java",
                $"No se pudo instalar {entry.Id}.{Environment.NewLine}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            entry.IsBusy = false;
            EndOperation();
        }
    }

    private async Task InstallSelectedFabricAsync()
    {
        var entry = SelectedVersion;
        if (entry is null || !entry.IsInstalled)
            return;

        var profile = ActiveStore.GetOrCreate(entry.Id);
        if (profile.ModLoader != JavaModLoaders.Fabric)
            return;

        entry.IsBusy = true;
        _operationCts = new CancellationTokenSource();
        BeginOperation("Instalando Fabric…", entry.Id);
        StatusMessage = $"Instalando Fabric para {entry.Id}…";
        var progress = CreateProgress();

        try
        {
            var loader = await ActiveService.InstallFabricAsync(entry.Id, progress, _operationCts.Token);
            profile.FabricLoaderVersion = loader ?? string.Empty;
            ActiveStore.SaveProfile(profile);
            entry.IsInstalled = true;
            ApplyEntryMetadata(entry);
            RefreshView();
            StatusMessage = $"Fabric {profile.FabricLoaderVersion} listo para {entry.Id}.";
            ToastHub.Success("Fabric instalado.");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Instalación de Fabric cancelada.";
            ToastHub.Warning("Instalación de Fabric cancelada.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo instalar Fabric para " + entry.Id);
            StatusMessage = "No se pudo instalar Fabric.";
            _dialogs.ShowError("No se pudo instalar Fabric",
                $"No se pudo instalar el cargador de Fabric para {entry.Id}.{Environment.NewLine}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            entry.IsBusy = false;
            EndOperation();
        }
    }

    private async Task PlayAsync(JavaVersionEntry? entry)
    {
        if (entry is null || !entry.IsInstalled)
            return;

        var profile = ActiveStore.GetOrCreate(entry.Id);
        _operationCts = new CancellationTokenSource();
        BeginOperation("Preparando el lanzamiento…", entry.Id);
        StatusMessage = $"Iniciando Minecraft {entry.Id}…";
        var progress = CreateProgress();
        var launched = false;

        try
        {
            if (profile.ModLoader == JavaModLoaders.Fabric &&
                !ActiveService.IsInstalled(JavaService.FabricVersionId(entry.Id)))
            {
                try
                {
                    var loader = await ActiveService.InstallFabricAsync(entry.Id, progress, _operationCts.Token);
                    profile.FabricLoaderVersion = loader ?? string.Empty;
                    ActiveStore.SaveProfile(profile);
                    ApplyEntryMetadata(entry);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogService.Error(ex, "No se pudo preparar Fabric para " + entry.Id);
                    StatusMessage = "No se pudo instalar Fabric antes de jugar.";
                    _dialogs.ShowError("No se pudo preparar Fabric",
                        $"No se pudo instalar Fabric para {entry.Id}.{Environment.NewLine}{Environment.NewLine}{ex.Message}");
                    return;
                }
            }

            var launchVersionId = GetLaunchVersionId(entry);
            if (!ActiveService.IsInstalled(launchVersionId))
            {
                StatusMessage = "La versión no está instalada correctamente.";
                _dialogs.ShowError("Versión incompleta",
                    $"Faltan archivos de {launchVersionId} en el workspace {WorkspaceName}.");
                return;
            }

            JavaSession? session;
            if (ActiveWorkspace.IsPremium)
            {
                if (!_profile.IsMicrosoftAccount || !_settings.IsMicrosoftConfigured)
                {
                    StatusMessage = "El workspace Premium necesita una cuenta Microsoft con Minecraft.";
                    _dialogs.ShowError("Cuenta Microsoft necesaria",
                        "Inicia sesión con Microsoft y configura el Client ID en Ajustes para jugar en el workspace Premium. " +
                        "No se usará un perfil local en este workspace.");
                    return;
                }

                session = await _profile.GetPremiumJavaSessionAsync(_operationCts.Token);
                if (session is null)
                {
                    StatusMessage = "No se pudo validar la sesión premium.";
                    _dialogs.ShowError("Sesión premium no disponible",
                        "No se pudo validar tu cuenta de Microsoft con Minecraft Services. " +
                        "Comprueba tu conexión y vuelve a iniciar sesión si es necesario.");
                    return;
                }
            }
            else
            {
                var localName = string.IsNullOrWhiteSpace(profile.PlayerName)
                    ? "Jugador"
                    : profile.PlayerName;
                session = new JavaSession
                {
                    IsPremium = false,
                    PlayerName = localName,
                    Uuid = JavaService.OfflineUuid(localName),
                    AccessToken = "0",
                    Notice = "Perfil local: solo modos de juego y servidores que admitan acceso sin cuenta."
                };
            }

            profile.LastPlayedAt = DateTimeOffset.Now;
            ActiveStore.SaveProfile(profile);
            ActiveStore.Config.LastVersionId = entry.Id;
            ActiveStore.SaveConfig();

            var gameDirectory = ActiveWorkspace.GetGameDirectory(profile);
            Directory.CreateDirectory(gameDirectory);
            Directory.CreateDirectory(ActiveWorkspace.GetModsDirectory(profile));

            var result = await ActiveService.LaunchAsync(
                launchVersionId,
                session,
                profile.MemoryMb,
                NullIfEmpty(profile.JavaPath),
                progress,
                _operationCts.Token,
                gameDirectory);

            launched = result.Success;
            StatusMessage = result.Message;
            if (result.Success)
            {
                ToastHub.Success(result.Message);
                ApplyEntryMetadata(entry);
                OnPropertyChanged(nameof(IsSelectedRunning));
                RaiseCommandStates();
            }
            else
            {
                _dialogs.ShowError("No se pudo iniciar Minecraft Java", result.Message);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Lanzamiento cancelado.";
            ToastHub.Warning("Lanzamiento cancelado.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo iniciar Minecraft Java " + entry.Id);
            StatusMessage = "No se pudo iniciar Minecraft.";
            _dialogs.ShowError("No se pudo iniciar Minecraft Java",
                $"No se pudo iniciar {entry.Id}.{Environment.NewLine}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            EndOperation();
        }

        if (!launched)
            return;

        if (_settings.Settings.CloseOnLaunch)
        {
            Application.Current?.Shutdown();
            return;
        }

        if (_settings.Settings.MinimizeOnLaunch && Application.Current?.MainWindow is { } window)
            window.WindowState = WindowState.Minimized;
    }

    private async Task DeleteAsync(JavaVersionEntry? entry)
    {
        if (entry is null)
            return;

        var launchVersionId = GetLaunchVersionId(entry);
        if (ActiveService.IsRunning(entry.Id) || ActiveService.IsRunning(launchVersionId))
        {
            ToastHub.Warning("Cierra Minecraft antes de eliminar esta versión.");
            return;
        }

        if (!_dialogs.Confirm("Eliminar versión de Minecraft",
                $"Se eliminarán los archivos de {entry.Id} del workspace {WorkspaceName}. " +
                "Los mundos, mods y la configuración del perfil se conservan en su carpeta independiente.",
                "Eliminar", danger: true))
            return;

        BeginOperation("Eliminando la versión…", entry.Id);
        try
        {
            await ActiveService.DeleteVersionFilesAsync(entry.Id);
            entry.IsInstalled = false;
            ApplyEntryMetadata(entry);
            RefreshView();
            NotifySelection();
            StatusMessage = $"Se eliminaron los archivos de {entry.Id} en {WorkspaceName}.";
            ToastHub.Success($"Versión {entry.Id} eliminada.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo eliminar la versión Java " + entry.Id);
            _dialogs.ShowError("No se pudo eliminar la versión",
                $"Cierra Minecraft y vuelve a intentarlo.{Environment.NewLine}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            EndOperation();
        }
    }

    private void ConfigureVersion(JavaVersionEntry? entry)
    {
        if (entry is null)
            return;

        SelectedVersion = entry;
        var profile = ActiveStore.GetOrCreate(entry.Id);
        StatusMessage = $"Configuración de {entry.Id} cargada: {profile.ModLoader}, {profile.MemoryMb} MB, " +
                        (ActiveWorkspace.IsPremium ? "cuenta Microsoft" : profile.PlayerName) + ".";
    }

    private void CloseGame()
    {
        if (SelectedVersion is null)
            return;

        ActiveService.Kill(GetLaunchVersionId(SelectedVersion));
        StatusMessage = $"Se solicitó cerrar Minecraft {SelectedVersion.Id}.";
        ToastHub.Success("Minecraft se está cerrando.");
    }

    private void CancelOperation()
    {
        if (_operationCts is null)
            return;
        try
        {
            _operationCts.Cancel();
        }
        catch
        {
            // ignored
        }
        StatusMessage = "Cancelando…";
    }

    private void BeginOperation(string stage, string detail)
    {
        IsBusy = true;
        ProgressStage = stage;
        ProgressDetail = detail;
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
    }

    private void EndOperation()
    {
        _operationCts?.Dispose();
        _operationCts = null;
        IsBusy = false;
        IsProgressIndeterminate = false;
        ProgressPercent = 0;
        OnPropertyChanged(nameof(IsSelectedRunning));
        RaiseCommandStates();
    }

    private Progress<JavaInstallProgress> CreateProgress() => new(report =>
    {
        ProgressStage = report.Stage;
        ProgressDetail = report.Detail;
        IsProgressIndeterminate = report.Indeterminate;
        ProgressPercent = report.Percent;
    });

    private void RefreshInstalledState()
    {
        foreach (var version in _all)
        {
            version.IsInstalled = ActiveService.IsInstalled(version.Id);
            ApplyEntryMetadata(version);
        }

        NotifyCounts();
        NotifySelection();
    }

    private void ApplyAllEntryMetadata()
    {
        foreach (var version in _all)
        {
            version.IsInstalled = ActiveService.IsInstalled(version.Id);
            ApplyEntryMetadata(version);
        }
    }

    private void ApplyEntryMetadata(JavaVersionEntry entry)
    {
        var profile = ActiveStore.Find(entry.Id);
        entry.HasProfile = profile is not null;
        entry.ProfileTypeLabel = WorkspaceName;
        entry.ProfilePlayerName = ActiveWorkspace.IsPremium
            ? (entry.HasProfile ? "Cuenta Microsoft" : "—")
            : profile?.PlayerName ?? "—";
        entry.ModLoaderLabel = GetEntryLoaderLabel(entry, profile);
    }

    private string GetEntryLoaderLabel(JavaVersionEntry entry, JavaProfile? profile)
    {
        if (profile?.ModLoader == JavaModLoaders.Fabric)
        {
            return ActiveService.IsInstalled(JavaService.FabricVersionId(entry.Id))
                ? JavaModLoaders.Fabric
                : "Fabric (pendiente)";
        }

        return JavaModLoaders.Vanilla;
    }

    private string GetLaunchVersionId(JavaVersionEntry entry)
    {
        var profile = ActiveStore.Find(entry.Id);
        return profile?.ModLoader == JavaModLoaders.Fabric
            ? JavaService.FabricVersionId(entry.Id)
            : entry.Id;
    }

    private bool IsVersionRunning(string versionId)
    {
        var entry = _all.FirstOrDefault(version => version.Id.Equals(versionId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return false;
        return ActiveService.IsRunning(GetLaunchVersionId(entry));
    }

    private void RefreshView()
    {
        Versions.Clear();
        foreach (var version in _all.Where(MatchesFilter))
            Versions.Add(version);

        OnPropertyChanged(nameof(HasVersions));
        OnPropertyChanged(nameof(VisibleCountLabel));
        RaiseCommandStates();
    }

    private bool MatchesFilter(JavaVersionEntry version)
    {
        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !version.Id.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return SelectedFilter switch
        {
            "Release" => version.Type == "release",
            "Snapshot" => version.Type == "snapshot",
            "Antiguas" => version.Type is "old_beta" or "old_alpha",
            "Instaladas" => version.IsInstalled,
            _ => true
        };
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(HasVersions));
        OnPropertyChanged(nameof(VisibleCountLabel));
        OnPropertyChanged(nameof(InstalledCountLabel));
        OnPropertyChanged(nameof(PremiumInstalledCountLabel));
        OnPropertyChanged(nameof(NoPremiumInstalledCountLabel));
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelectedInstalled));
        OnPropertyChanged(nameof(IsSelectedRunning));
        OnPropertyChanged(nameof(IsSelectedFabricPending));
        OnPropertyChanged(nameof(IsSelectedFabricReady));
        OnPropertyChanged(nameof(SelectedVersionLabel));
        OnPropertyChanged(nameof(SelectedVersionType));
        OnPropertyChanged(nameof(SelectedVersionDate));
        OnPropertyChanged(nameof(SelectedProfileSummary));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(MemoryMb));
        OnPropertyChanged(nameof(MemoryLabel));
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(JavaPath));
        OnPropertyChanged(nameof(JavaPathHint));
        OnPropertyChanged(nameof(IsJavaPathCustom));
        OnPropertyChanged(nameof(GameDirectory));
        OnPropertyChanged(nameof(GameDirectoryHint));
        OnPropertyChanged(nameof(IsGameDirectoryCustom));
        OnPropertyChanged(nameof(ResolvedGameDirectory));
        OnPropertyChanged(nameof(ModLoader));
        OnPropertyChanged(nameof(FabricLoaderVersion));
        OnPropertyChanged(nameof(FabricLoaderLabel));
    }

    private void RaiseCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        InstallCommand.RaiseCanExecuteChanged();
        PlayCommand.RaiseCanExecuteChanged();
        ConfigureCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        PrimaryCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        CloseGameCommand.RaiseCanExecuteChanged();
        InstallFabricCommand.RaiseCanExecuteChanged();
        DetectJavaCommand.RaiseCanExecuteChanged();
    }

    private void OnRunningChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(() => OnRunningChanged());
            return;
        }

        OnPropertyChanged(nameof(IsSelectedRunning));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        RaiseCommandStates();
    }

    private void OnProfileChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(() => UpdatePremiumStatus());
            return;
        }

        UpdatePremiumStatus();
    }

    private void UpdatePremiumStatus()
    {
        if (ActiveWorkspace.IsPremium)
        {
            PremiumStatus = !HasPremiumAccount
                ? "Workspace Premium: inicia sesión con Microsoft y configura el Client ID. No se usará ningún perfil local aquí."
                : $"Sesión premium disponible: {_profile.DisplayName}. La cuenta, tokens y skins oficiales se usan sin modificarlos.";
        }
        else
        {
            var localName = ActiveProfile?.PlayerName ?? ActiveStore.Config.DefaultPlayerName;
            PremiumStatus = $"Perfil local: {localName}. Se usa solo en modos de juego y servidores que admitan acceso sin cuenta.";
        }

        OnPropertyChanged(nameof(PremiumStatus));
        OnPropertyChanged(nameof(HasPremiumAccount));
        OnPropertyChanged(nameof(AccountWarningVisible));
        OnPropertyChanged(nameof(IsPlayerNameEditable));
    }

    private void InitializeWorkspaceDefaults(JavaProfileStore store, string defaultPlayerName)
    {
        if (store.WasInitialized)
            return;

        store.Config.DefaultPlayerName = string.IsNullOrWhiteSpace(defaultPlayerName)
            ? "Jugador"
            : defaultPlayerName;
        store.Config.DefaultMemoryMb = _settings.Settings.Java.MemoryMb;
        store.Config.DefaultJavaPath = _settings.Settings.Java.JavaPath;
        store.SaveConfig();
    }

    private void BrowseJava()
    {
        var file = _dialogs.PickJavaExecutable();
        if (file is not null)
            JavaPath = file;
    }

    private void BrowseGameFolder()
    {
        var folder = _dialogs.PickFolder("Selecciona la carpeta de partidas");
        if (folder is not null)
            GameDirectory = folder;
    }

    private void OpenProfileMods()
    {
        var directory = SelectedVersion is null
            ? ActiveWorkspace.GamesDirectory
            : ActiveWorkspace.GetModsDirectory(ActiveProfile ?? ActiveStore.GetOrCreate(SelectedVersion.Id));
        FileOps.OpenFolder(directory);
    }

    private void OpenProfileSaves()
    {
        var directory = SelectedVersion is null
            ? ActiveWorkspace.GamesDirectory
            : ActiveWorkspace.GetSavesDirectory(ActiveProfile ?? ActiveStore.GetOrCreate(SelectedVersion.Id));
        FileOps.OpenFolder(directory);
    }

    private void ShowError(Exception exception)
    {
        LogService.Error(exception, "Error en una acción de Minecraft Java.");
        _dialogs.ShowError("Error inesperado", exception.Message);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _premiumJava.RunningChanged -= OnRunningChanged;
        _noPremiumJava.RunningChanged -= OnRunningChanged;
        _profile.PropertyChanged -= OnProfileChanged;
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = null;
    }
}
