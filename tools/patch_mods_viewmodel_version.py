import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/ViewModels/ModsCenterViewModel.cs'
s = read(p)

s = s.replace('    private ModrinthProject? _selectedProject;\n',
              '    private ModrinthProject? _selectedProject;\n    private ModrinthVersion? _selectedModVersion;\n    private bool _isLoadingVersions;\n    private string _targetFolder = string.Empty;\n', 1)

s = s.replace('        GameVersions = [];\n        Loaders = [];\n',
              '        GameVersions = [];\n        Loaders = [];\n        ModVersions = [];\n', 1)

s = s.replace('        InstallCommand = new AsyncRelayCommand<ModrinthProject>(InstallAsync, project => !IsInstalling && project is not null, OnError);\n',
              '        InstallCommand = new AsyncRelayCommand<ModrinthProject>(InstallAsync, project => !IsInstalling && project is not null, OnError);\n        LoadVersionsCommand = new AsyncRelayCommand<ModrinthProject>(LoadVersionsAsync,\n            project => !IsLoadingVersions && !IsInstalling && project is not null, OnError);\n        InstallSelectedVersionCommand = new AsyncRelayCommand(InstallSelectedVersionAsync,\n            () => !IsInstalling && SelectedProject is not null && SelectedModVersion is not null, OnError);\n', 1)

s = s.replace('    public ObservableCollection<string> Loaders { get; }\n',
              '    public ObservableCollection<string> Loaders { get; }\n    public ObservableCollection<ModrinthVersion> ModVersions { get; }\n', 1)

s = s.replace('    public AsyncRelayCommand<ModrinthProject> InstallCommand { get; }\n',
              '    public AsyncRelayCommand<ModrinthProject> InstallCommand { get; }\n    public AsyncRelayCommand<ModrinthProject> LoadVersionsCommand { get; }\n    public AsyncRelayCommand InstallSelectedVersionCommand { get; }\n', 1)

old = '''    public ModrinthProject? SelectedProject
    {
        get => _selectedProject;
        set => SetProperty(ref _selectedProject, value);
    }
'''
new = '''    public ModrinthProject? SelectedProject
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
'''
assert old in s
s = s.replace(old, new, 1)

s = s.replace('''            OnPropertyChanged(nameof(CompatibilitySummary));
            _ = SearchAsync(reset: true);
        }
    }

    public string SelectedLoader''', '''            OnPropertyChanged(nameof(CompatibilitySummary));
            ClearModVersions();
            _ = SearchAsync(reset: true);
        }
    }

    public string SelectedLoader''', 1)
s = s.replace('''            OnPropertyChanged(nameof(CompatibilitySummary));
            _ = SearchAsync(reset: true);
        }
    }

    public string SelectedCategory''', '''            OnPropertyChanged(nameof(CompatibilitySummary));
            ClearModVersions();
            _ = SearchAsync(reset: true);
        }
    }

    public string SelectedCategory''', 1)

s = s.replace('''            InstallCommand.RaiseCanExecuteChanged();
            InstallModpackCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
''', '''            InstallCommand.RaiseCanExecuteChanged();
            InstallSelectedVersionCommand.RaiseCanExecuteChanged();
            LoadVersionsCommand.RaiseCanExecuteChanged();
            InstallModpackCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
''', 1)

start = s.index('    private async Task InstallAsync(ModrinthProject? project)')
end = s.index('    private async Task InstallModpackAsync(', start)
new_methods = '''    private async Task InstallAsync(ModrinthProject? project)
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
                ProgressEta = p.Eta is not null ? $"~{p.Eta:hh\\\\:mm\\\\:ss}" : string.Empty;
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

'''
s = s[:start] + new_methods + s[end:]

old = '''            ActiveProfileLabel = profile is null
                ? workspace.DisplayName
                : $"{workspace.DisplayName} · {profile.VersionId} · {profile.ModLoader}";
'''
new = old + '''            TargetFolder = version is null
                ? "Sin versión Java seleccionada"
                : workspace.GetModsDirectory(profile ?? _java.ActiveStore.GetOrCreate(version.Id));
'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)
