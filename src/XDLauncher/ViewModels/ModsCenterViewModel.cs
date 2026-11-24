using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Estado real del centro de mods: catálogo Modrinth, biblioteca por perfil y descargas.</summary>
public sealed class ModsCenterViewModel : ObservableObject
{
    private readonly ModrinthService _modrinth = new();
    private readonly ModInstallService _installer;
    private readonly JavaViewModel _java;
    private readonly DialogService _dialogs;

    private string _searchText = string.Empty;
    private string _selectedGameVersion = "1.20.1";
    private string _selectedLoader = JavaModLoaders.Fabric;
    private string _selectedCategory = "Todas";
    private string _selectedSort = "Popularidad";
    private string _statusMessage = "Listo.";
    private bool _isLoading;
    private bool _isInstalling;
    private int _offset;
    private int _total;
    private ModrinthProject? _selectedProject;
    private ModrinthVersion? _selectedModVersion;
    private bool _isLoadingVersions;
    private string _targetFolder = string.Empty;
    private string _progressStage = string.Empty;
    private string _progressDetail = string.Empty;
    private double _progressPercent;
    private string _progressSpeed = string.Empty;
    private string _progressEta = string.Empty;
    private string _activeProfileLabel = string.Empty;

    public ModsCenterViewModel(JavaViewModel java, DialogService dialogs)
    {
        _java = java;
        _dialogs = dialogs;
        _installer = new ModInstallService(_modrinth);

        Projects = [];
        Library = [];
        Modpacks = [];
        ModpackLibrary = [];
        GameVersions = [];
        Loaders = [];
        ModVersions = [];
        Categories =
        [
            "Todas", "technology", "magic", "adventure", "combat", "mobs",
            "worldgen", "building", "optimization", "decoration", "exploration", "utility"
        ];
        SortOptions = ["Popularidad", "Recientes", "Relevancia"];

        SearchCommand = new AsyncRelayCommand(() => SearchAsync(reset: true), () => !IsLoading, OnError);
        LoadMoreCommand = new AsyncRelayCommand(() => SearchAsync(reset: false), () => !IsLoading && Projects.Count < Total, OnError);
        InstallCommand = new AsyncRelayCommand<ModrinthProject>(InstallAsync, project => !IsInstalling && project is not null, OnError);
        LoadVersionsCommand = new AsyncRelayCommand<ModrinthProject>(LoadVersionsAsync,
            project => !IsLoadingVersions && !IsInstalling && project is not null, OnError);
        InstallSelectedVersionCommand = new AsyncRelayCommand(InstallSelectedVersionAsync,
            () => !IsInstalling && SelectedProject is not null && SelectedModVersion is not null, OnError);
        RefreshCommand = new AsyncRelayCommand(async () => { await SearchAsync(reset: true); await RefreshLibraryAsync(); }, () => !IsLoading, OnError);
        ToggleCommand = new RelayCommand<InstalledMod>(Toggle, mod => mod is not null);
        RemoveCommand = new RelayCommand<InstalledMod>(Remove, mod => mod is not null);
        UpdateCommand = new AsyncRelayCommand<InstalledMod>(UpdateAsync, mod => !IsInstalling && mod is not null, OnError);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !IsLoading, OnError);
        OpenModsFolderCommand = new RelayCommand(OpenModsFolder);
        InstallModpackCommand = new AsyncRelayCommand<ModrinthProject>(InstallModpackAsync,
            pack => !IsInstalling && pack is not null, OnError);
        OpenModpackFolderCommand = new RelayCommand<InstalledModpack>(pack =>
        {
            if (pack is not null)
                _installer.OpenModpackFolder(pack, _java.ActiveWorkspace);
        });
        OpenWebsiteCommand = new RelayCommand<string>(url =>
        {
            if (!string.IsNullOrWhiteSpace(url))
                FileOps.OpenUrl(url);
        });
        SelectProfileCommand = new RelayCommand(RefreshLibrary);

        _ = InitializeAsync();
    }

    public ObservableCollection<ModrinthProject> Projects { get; }
    public ObservableCollection<InstalledMod> Library { get; }
    public ObservableCollection<ModrinthProject> Modpacks { get; }
    public ObservableCollection<InstalledModpack> ModpackLibrary { get; }
    public ObservableCollection<string> GameVersions { get; }
    public ObservableCollection<string> Loaders { get; }
    public ObservableCollection<ModrinthVersion> ModVersions { get; }
    public ObservableCollection<JavaVersionEntry> JavaVersions => _java.Versions;
    public event Action? OpenJavaRequested;
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<string> SortOptions { get; }

    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand LoadMoreCommand { get; }
    public AsyncRelayCommand<ModrinthProject> InstallCommand { get; }
    public AsyncRelayCommand<ModrinthProject> LoadVersionsCommand { get; }
    public AsyncRelayCommand InstallSelectedVersionCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand<InstalledMod> ToggleCommand { get; }
    public RelayCommand<InstalledMod> RemoveCommand { get; }
    public AsyncRelayCommand<InstalledMod> UpdateCommand { get; }
    public AsyncRelayCommand CheckUpdatesCommand { get; }
    public RelayCommand OpenModsFolderCommand { get; }
    public AsyncRelayCommand<ModrinthProject> InstallModpackCommand { get; }
    public RelayCommand<InstalledModpack> OpenModpackFolderCommand { get; }
    public RelayCommand<string> OpenWebsiteCommand { get; }
    public RelayCommand SelectProfileCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string SelectedGameVersion
    {
        get => _selectedGameVersion;
        set
        {
            if (!SetProperty(ref _selectedGameVersion, value))
                return;
            OnPropertyChanged(nameof(CompatibilitySummary));
            ClearModVersions();
            _ = SearchAsync(reset: true);
        }
    }

    public string SelectedLoader
    {
        get => _selectedLoader;
        set
        {
            if (!SetProperty(ref _selectedLoader, value))
                return;
            OnPropertyChanged(nameof(CompatibilitySummary));
            ClearModVersions();
            _ = SearchAsync(reset: true);
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value))
                return;
            _ = SearchAsync(reset: true);
        }
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (!SetProperty(ref _selectedSort, value))
                return;
            _ = SearchAsync(reset: true);
        }
    }

    public ModrinthProject? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (!SetProperty(ref _selectedProject, value))
                return;
            ClearModVersions();
            if (value is not null)
                _ = LoadVersionsAsync(value);
            InstallSelectedVersionCommand.RaiseCanExecuteChanged();
        }
    }

    public JavaVersionEntry? SelectedJavaVersion
    {
        get => _java.SelectedVersion;
        set
        {
            if (ReferenceEquals(_java.SelectedVersion, value))
                return;
            _java.SelectedVersion = value;
            OnPropertyChanged(nameof(SelectedJavaVersion));
            OnPropertyChanged(nameof(TargetFolder));
            OnPropertyChanged(nameof(CompatibilitySummary));
            ClearModVersions();
            if (value is not null && SelectedProject is not null)
                _ = LoadVersionsAsync(SelectedProject);
            _ = RefreshLibraryAsync();
        }
    }

    public bool HasJavaVersion => _java.SelectedVersion is not null;

    public ModrinthVersion? SelectedModVersion
    {
        get => _selectedModVersion;
        set
        {
            if (!SetProperty(ref _selectedModVersion, value))
                return;
            InstallSelectedVersionCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsLoadingVersions
    {
        get => _isLoadingVersions;
        private set
        {
            if (!SetProperty(ref _isLoadingVersions, value))
                return;
            LoadVersionsCommand.RaiseCanExecuteChanged();
            InstallSelectedVersionCommand.RaiseCanExecuteChanged();
        }
    }

    public string TargetFolder
    {
        get => _targetFolder;
        private set => SetProperty(ref _targetFolder, value);
    }

    public bool HasModVersions => ModVersions.Count > 0;

    private void ClearModVersions()
    {
        ModVersions.Clear();
        SelectedModVersion = null;
        OnPropertyChanged(nameof(HasModVersions));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
                return;
            SearchCommand.RaiseCanExecuteChanged();
            LoadMoreCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();
            CheckUpdatesCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set
        {
            if (!SetProperty(ref _isInstalling, value))
                return;
            InstallCommand.RaiseCanExecuteChanged();
            InstallSelectedVersionCommand.RaiseCanExecuteChanged();
            LoadVersionsCommand.RaiseCanExecuteChanged();
            InstallModpackCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
        }
    }

    public int Total
    {
        get => _total;
        private set
        {
            SetProperty(ref _total, value);
            OnPropertyChanged(nameof(ResultSummary));
            LoadMoreCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasResults => Projects.Count > 0;
    public string ResultSummary => IsLoading ? "Consultando Modrinth…" : $"{Projects.Count} de {Total} resultados";
    public string CompatibilitySummary => $"{SelectedGameVersion} · {SelectedLoader}";
    public bool CanLoadMore => Projects.Count < Total && !IsLoading;

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

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string ProgressSpeed
    {
        get => _progressSpeed;
        private set => SetProperty(ref _progressSpeed, value);
    }

    public string ProgressEta
    {
        get => _progressEta;
        private set => SetProperty(ref _progressEta, value);
    }

    public bool ShowProgress => IsInstalling;

    public string ActiveProfileLabel
    {
        get => _activeProfileLabel;
        private set => SetProperty(ref _activeProfileLabel, value);
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _java.EnsureLoadedAsync();
            await RunOnUiAsync(() =>
            {
                OnPropertyChanged(nameof(JavaVersions));
                OnPropertyChanged(nameof(HasJavaVersion));
                OnPropertyChanged(nameof(SelectedJavaVersion));
                OnPropertyChanged(nameof(TargetFolder));
            });

            var versions = await _modrinth.GetGameVersionsAsync();
            await RunOnUiAsync(() =>
            {
                GameVersions.Clear();
                foreach (var version in versions.Take(120))
                    GameVersions.Add(version);
                if (!GameVersions.Contains(SelectedGameVersion) && GameVersions.Count > 0)
                    SelectedGameVersion = GameVersions.First();
            });

            var loaders = await _modrinth.GetLoadersAsync();
            await RunOnUiAsync(() =>
            {
                Loaders.Clear();
                foreach (var loader in loaders)
                    Loaders.Add(loader);
                if (Loaders.Count > 0 && !Loaders.Contains(SelectedLoader))
                    SelectedLoader = Loaders.First();
            });

            if (_java.SelectedVersion is not null && !string.IsNullOrWhiteSpace(_java.SelectedVersion.Id))
                await RunOnUiAsync(() => SelectedGameVersion = _java.SelectedVersion.Id);

            await SearchAsync(reset: true);
            await RefreshLibraryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo conectar con Modrinth: " + ex.Message;
            LogService.Warn("Modrinth no disponible al inicializar: " + ex.Message);
            await SearchAsync(reset: true);
        }
    }

    private async Task SearchAsync(bool reset)
    {
        IsLoading = true;
        StatusMessage = "Buscando mods en Modrinth…";
        try
        {
            if (reset)
            {
                _offset = 0;
                await RunOnUiAsync(() => Projects.Clear());
            }

            var page = await _modrinth.SearchAsync(
                SearchText,
                SelectedGameVersion,
                SelectedLoader,
                SelectedCategory == "Todas" ? null : SelectedCategory,
                SelectedSort,
                _offset,
                24);

            _offset += page.Projects.Count;
            Total = page.Total;
            await RunOnUiAsync(() =>
            {
                foreach (var project in page.Projects)
                    Projects.Add(project);
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(CanLoadMore));
            });
            StatusMessage = page.Total == 0
                ? "No hay mods compatibles con los filtros seleccionados."
                : $"Mostrando {Projects.Count} de {page.Total} mods.";

            var modpackPage = await _modrinth.SearchAsync(
                SearchText,
                null,
                null,
                null,
                SelectedSort,
                0,
                24,
                CancellationToken.None,
                projectType: "modpack");
            var compatiblePacks = modpackPage.Projects.Where(pack =>
                pack.Versions.Any(v => v.StartsWith(SelectedGameVersion, StringComparison.OrdinalIgnoreCase)) ||
                pack.Categories.Any(c => string.Equals(c, SelectedLoader, StringComparison.OrdinalIgnoreCase))).ToList();
            await RunOnUiAsync(() =>
            {
                Modpacks.Clear();
                foreach (var pack in compatiblePacks)
                    Modpacks.Add(pack);
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task InstallAsync(ModrinthProject? project)
        => await InstallCoreAsync(project, null);

    private async Task InstallSelectedVersionAsync()
    {
        if (SelectedProject is null || SelectedModVersion is null)
            return;
        await InstallCoreAsync(SelectedProject, SelectedModVersion);
    }

    private async Task LoadVersionsAsync(ModrinthProject? project)
    {
        if (project is null)
            return;

        IsLoadingVersions = true;
        ClearModVersions();
        StatusMessage = $"Consultando versiones de {project.Name}…";
        try
        {
            var (_, _, gameVersion, loader, _) = GetTargetContext();
            var versions = await _modrinth.GetVersionsAsync(project.Id, gameVersion, loader);
            await RunOnUiAsync(() =>
            {
                foreach (var version in versions
                             .Where(v => ModrinthService.IsCompatible(v, gameVersion, loader))
                             .OrderByDescending(v => v.DatePublished, StringComparer.Ordinal))
                    ModVersions.Add(version);
                if (ModVersions.Count > 0)
                    SelectedModVersion = ModVersions[0];
                OnPropertyChanged(nameof(HasModVersions));
                StatusMessage = $"{ModVersions.Count} versión(es) disponibles para {project.Name}.";
            });
        }
        finally
        {
            IsLoadingVersions = false;
            InstallSelectedVersionCommand.RaiseCanExecuteChanged();
        }
    }

    private (JavaWorkspace Workspace, JavaProfile Profile, string GameVersion, string Loader, string ModsDirectory) GetTargetContext()
    {
        var version = _java.SelectedVersion
            ?? throw new InvalidOperationException("Selecciona una versión de Minecraft Java antes de instalar mods.");
        var workspace = _java.ActiveWorkspace;
        var profile = _java.ActiveProfile ?? _java.ActiveStore.GetOrCreate(version.Id);
        var gameVersion = string.IsNullOrWhiteSpace(profile.VersionId) ? version.Id : profile.VersionId;
        var loader = string.IsNullOrWhiteSpace(profile.ModLoader) || profile.ModLoader == JavaModLoaders.Vanilla
            ? (string.IsNullOrWhiteSpace(SelectedLoader) ? JavaModLoaders.Fabric : SelectedLoader)
            : profile.ModLoader;
        workspace.EnsureProfileDirectories(profile);
        return (workspace, profile, gameVersion, loader, workspace.GetModsDirectory(profile));
    }

    private async Task InstallCoreAsync(ModrinthProject? project, ModrinthVersion? selectedVersion)
    {
        if (project is null)
            return;

        IsInstalling = true;
        StatusMessage = $"Instalando {project.Name}…";
        try
        {
            var (workspace, profile, gameVersion, loader, modsDirectory) = GetTargetContext();
            TargetFolder = modsDirectory;

            var progress = new Progress<ModInstallProgress>(p => RunOnUi(() =>
            {
                ProgressStage = p.Stage;
                ProgressDetail = p.Detail;
                ProgressPercent = p.Percent ?? p.OverallPercent;
                ProgressSpeed = p.BytesPerSecond is > 0 ? FormatSpeed(p.BytesPerSecond.Value) : string.Empty;
                ProgressEta = p.Eta is not null ? $"~{p.Eta:hh\\:mm\\:ss}" : string.Empty;
            }));

            var result = await _installer.InstallAsync(project, gameVersion, loader, workspace, profile,
                includeOptionalDependencies: false, progress, CancellationToken.None, selectedVersion);

            await RefreshLibraryAsync();
            StatusMessage = result.Installed.Count > 0
                ? $"Instalado: {string.Join(", ", result.Installed.Select(m => m.Name))} en {modsDirectory}."
                : "El mod ya estaba instalado y verificado.";
            ToastHub.Success(StatusMessage);
        }
        finally
        {
            IsInstalling = false;
            ProgressStage = string.Empty;
            ProgressDetail = string.Empty;
            ProgressSpeed = string.Empty;
            ProgressEta = string.Empty;
            ProgressPercent = 0;
            OnPropertyChanged(nameof(ShowProgress));
            InstallCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task InstallModpackAsync(ModrinthProject? pack)
    {
        if (pack is null)
            return;

        IsInstalling = true;
        StatusMessage = $"Instalando modpack {pack.Name}…";
        try
        {
            var progress = new Progress<ModInstallProgress>(p => RunOnUi(() =>
            {
                ProgressStage = p.Stage;
                ProgressDetail = p.Detail;
                ProgressPercent = p.Percent ?? p.OverallPercent;
                ProgressSpeed = p.BytesPerSecond is > 0 ? FormatSpeed(p.BytesPerSecond.Value) : string.Empty;
                ProgressEta = p.Eta is not null ? $"~{p.Eta:hh\\:mm\\:ss}" : string.Empty;
            }));

            var installed = await _installer.InstallModpackAsync(pack, SelectedGameVersion,
                _java.ActiveWorkspace, _java.ActiveStore, progress, CancellationToken.None);
            await RefreshLibraryAsync();
            StatusMessage = $"Modpack instalado: {installed.Name} {installed.VersionLabel} · {installed.MinecraftVersion}/{installed.Loader}.";
            ToastHub.Success(StatusMessage);
        }
        finally
        {
            IsInstalling = false;
            ProgressStage = string.Empty;
            ProgressDetail = string.Empty;
            ProgressSpeed = string.Empty;
            ProgressEta = string.Empty;
            ProgressPercent = 0;
            OnPropertyChanged(nameof(ShowProgress));
            InstallModpackCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task RefreshLibraryAsync()
    {
        var workspace = _java.ActiveWorkspace;
        var version = _java.SelectedVersion;
        var profile = version is null ? null : _java.ActiveStore.GetOrCreate(version.Id);
        await RunOnUiAsync(() =>
        {
            Library.Clear();
            foreach (var mod in profile is null ? _installer.GetLibrary(workspace) : _installer.GetProfileLibrary(workspace, profile))
                Library.Add(mod);
            ModpackLibrary.Clear();
            foreach (var pack in _installer.GetModpackLibrary(workspace))
                ModpackLibrary.Add(pack);
            ActiveProfileLabel = profile is null
                ? workspace.DisplayName
                : $"{workspace.DisplayName} · {profile.VersionId} · {profile.ModLoader}";
            TargetFolder = version is null
                ? "Sin versión Java seleccionada"
                : workspace.GetModsDirectory(profile ?? _java.ActiveStore.GetOrCreate(version.Id));
        });
    }

    private void Toggle(InstalledMod? mod)
    {
        if (mod is null)
            return;
        try
        {
            _installer.SetEnabled(mod, _java.ActiveWorkspace, !mod.IsEnabled);
            StatusMessage = $"'{mod.Name}' {(mod.IsEnabled ? "activado" : "desactivado")}.";
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
    }

    private void Remove(InstalledMod? mod)
    {
        if (mod is null)
            return;
        try
        {
            _installer.Remove(mod, _java.ActiveWorkspace);
            Library.Remove(mod);
            StatusMessage = $"'{mod.Name}' eliminado de la biblioteca.";
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
    }

    private async Task UpdateAsync(InstalledMod? mod)
    {
        if (mod is null)
            return;
        IsInstalling = true;
        StatusMessage = $"Actualizando {mod.Name}…";
        try
        {
            var progress = new Progress<ModInstallProgress>(p => RunOnUi(() =>
            {
                ProgressStage = p.Stage;
                ProgressDetail = p.Detail;
                ProgressPercent = p.Percent ?? p.OverallPercent;
            }));
            await _installer.UpdateAsync(mod, _java.ActiveWorkspace, progress, CancellationToken.None);
            await RefreshLibraryAsync();
            ToastHub.Success($"'{mod.Name}' actualizado. Se conservó una copia anterior.");
        }
        finally
        {
            IsInstalling = false;
            ProgressStage = string.Empty;
            ProgressPercent = 0;
            OnPropertyChanged(nameof(ShowProgress));
        }
    }

    private async Task CheckUpdatesAsync()
    {
        IsLoading = true;
        StatusMessage = "Comprobando actualizaciones de mods…";
        try
        {
            var workspace = _java.ActiveWorkspace;
            var version = _java.SelectedVersion;
            var profile = version is null ? null : _java.ActiveStore.GetOrCreate(version.Id);
            if (profile is null)
                return;
            var updates = await _installer.CheckUpdatesAsync(workspace, profile);
            StatusMessage = updates.Count == 0
                ? "Todos los mods están actualizados."
                : $"{updates.Count} mod(s) tienen actualizaciones compatibles.";
            ToastHub.Info(StatusMessage);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenModsFolder()
    {
        var version = _java.SelectedVersion;
        if (version is null)
        {
            if (_dialogs.Confirm("Centro de mods",
                    "Selecciona una versión Java para abrir su carpeta de mods. ¿Quieres ir a Minecraft Java?",
                    "Ir a Java", "Ahora no"))
                OpenJavaRequested?.Invoke();
            return;
        }
        var workspace = _java.ActiveWorkspace;
        _installer.OpenModsFolder(workspace, _java.ActiveStore.GetOrCreate(version.Id));
    }

    private void RefreshLibrary()
        => _ = RefreshLibraryAsync();

    private void OnError(Exception exception)
    {
        LogService.Error(exception, "Error en el centro de mods.");
        StatusMessage = exception.Message;
        _dialogs.ShowError("Centro de mods", exception.Message);
    }

    private static string FormatSpeed(double bytesPerSecond)
        => bytesPerSecond >= 1024 * 1024
            ? $"{bytesPerSecond / 1024 / 1024:0.0} MB/s"
            : $"{bytesPerSecond / 1024:0} KB/s";

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    private static async Task RunOnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            await dispatcher.InvokeAsync(action);
    }
}
