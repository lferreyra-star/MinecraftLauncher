using System.Collections.ObjectModel;
using System.Windows;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Installation detection, activation, verification and update state.</summary>
public sealed class GameState : ObservableObject
{
    private readonly GameService _game;
    private readonly CatalogState _catalog;
    private readonly SettingsState _settings;
    private readonly DialogService _dialogs;

    private GameSnapshot _snapshot = new();
    private GameInstallation? _selected;
    private UpdateInfo _update = new();
    private bool _isRefreshing;
    private bool _isLaunching;
    private string _statusMessage = "Buscando Minecraft en este equipo…";

    public GameState(GameService game, CatalogState catalog, SettingsState settings, DialogService dialogs)
    {
        _game = game;
        _catalog = catalog;
        _settings = settings;
        _dialogs = dialogs;

        Installations = [];

        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(), onError: OnError);
        LaunchCommand = new AsyncRelayCommand(LaunchAsync, () => IsInstalled && !IsLaunching, OnError);
        VerifyCommand = new AsyncRelayCommand(VerifyAsync, () => IsInstalled, OnError);
        OpenStoreCommand = new RelayCommand(() => _game.OpenStore());
        OpenUpdatesCommand = new RelayCommand(() => _game.OpenUpdates());
        SelectInstallationCommand = new RelayCommand(parameter =>
        {
            if (parameter is GameInstallation installation)
                Selected = installation;
        });
        RepairComponentCommand = new RelayCommand(parameter =>
        {
            if (parameter is ComponentCheck { StoreProductId: { Length: > 0 } productId })
                _game.OpenComponentStore(productId);
        });
        CopyVersionCommand = new RelayCommand(() =>
        {
            if (Selected is not null)
                FileOps.CopyText(Selected.Version);
        });
        OpenInstallFolderCommand = new RelayCommand(OpenInstallFolder);
        OpenUserDataFolderCommand = new RelayCommand(OpenUserDataFolder);
    }

    public ObservableCollection<GameInstallation> Installations { get; }

    public GameInstallation? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                Update = _game.ComputeUpdate(value, _catalog.Product);
                NotifyDerived();
            }
        }
    }

    public UpdateInfo Update
    {
        get => _update;
        private set
        {
            if (SetProperty(ref _update, value))
            {
                OnPropertyChanged(nameof(UpdateStatus));
                OnPropertyChanged(nameof(UpdateDetail));
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetProperty(ref _isRefreshing, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(StatusHeadline));
            }
        }
    }

    public bool IsLaunching
    {
        get => _isLaunching;
        private set
        {
            if (SetProperty(ref _isLaunching, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(PlayLabel));
                OnPropertyChanged(nameof(StatusHeadline));
                LaunchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy => IsRefreshing || IsLaunching;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsInstalled => Selected is not null;
    public bool HasInstallation => IsInstalled;
    public bool IsGameRunning => _snapshot.IsGameRunning || Selected?.IsRunning == true;
    public bool HasMultipleChannels => Installations.Count > 1;
    public bool HasMissingComponents => Selected?.HasMissingComponents == true;
    public bool HasUpdate => Update.Availability == UpdateAvailability.Available;
    public bool IsUpToDate => Update.Availability == UpdateAvailability.UpToDate;

    public string DisplayName => Selected?.DisplayName ?? "Minecraft: Bedrock Edition";
    public string VersionLabel => Selected?.Version ?? "No detectada";
    public string ChannelLabel => Selected?.ChannelLabel ?? "—";
    public string InstallDateLabel => Selected is null
        ? "—"
        : Selected.InstalledDate.ToLocalTime().ToString("dd MMM yyyy");
    public string PackageStatusLabel => Selected?.PackageStatus ?? "Sin paquete";
    public string StatusHeadline => IsLaunching
        ? "Iniciando Minecraft…"
        : IsRefreshing
            ? "Comprobando instalación…"
            : !IsInstalled
                ? "Minecraft no está instalado"
                : IsGameRunning
                    ? "Minecraft está en ejecución"
                    : Selected!.IsHealthy
                        ? "Listo para jugar"
                        : "La instalación requiere atención";

    public string StatusDetail
    {
        get
        {
            if (!IsInstalled)
                return "Instala Minecraft desde Microsoft Store para poder jugar con una licencia legítima.";
            if (IsGameRunning)
                return "Puedes volver al juego en cualquier momento desde este launcher.";
            if (!Selected!.IsHealthy)
                return "Windows informa un problema con el paquete. Repáralo desde Microsoft Store.";
            return HasMissingComponents
                ? "Faltan componentes oficiales. Instálalos desde Microsoft Store."
                : "Instalación oficial detectada y verificada por Windows.";
        }
    }

    public string PlayLabel => IsLaunching
        ? "Iniciando…"
        : IsGameRunning ? "Volver al juego" : "Jugar ahora";

    public string PlayGlyph => IsGameRunning ? "\uE7C4" : "\uE768";
    public string UpdateStatus => Update.StatusText;
    public string UpdateDetail => Update.Detail;
    public string LastCheckLabel => $"Última comprobación: {_snapshot.DetectedAt.ToLocalTime():HH:mm}";
    public int MissingComponentsCount => Selected?.MissingRequiredCount ?? 0;
    public string ComponentsSummary => Selected is null
        ? "Sin datos"
        : $"{Selected.Components.Count(c => c.IsInstalled)} de {Selected.Components.Count} componentes disponibles";

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand LaunchCommand { get; }
    public AsyncRelayCommand VerifyCommand { get; }
    public RelayCommand OpenStoreCommand { get; }
    public RelayCommand OpenUpdatesCommand { get; }
    public RelayCommand SelectInstallationCommand { get; }
    public RelayCommand RepairComponentCommand { get; }
    public RelayCommand CopyVersionCommand { get; }
    public RelayCommand OpenInstallFolderCommand { get; }
    public RelayCommand OpenUserDataFolderCommand { get; }

    public event Action<LaunchResult>? Launched;

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        StatusMessage = "Consultando el sistema de paquetes de Windows…";
        try
        {
            var snapshot = await _game.DetectAsync();
            _snapshot = snapshot;

            var previous = Selected;
            Installations.Clear();
            foreach (var installation in snapshot.Installations)
                Installations.Add(installation);

            var preferredChannel = _settings.Settings.PreferredChannel == "Preview"
                ? InstallationChannel.Preview
                : InstallationChannel.Retail;

            Selected = Installations.FirstOrDefault(i => i.FamilyName.Equals(
                           previous?.FamilyName, StringComparison.OrdinalIgnoreCase))
                       ?? Installations.FirstOrDefault(i => i.Channel == preferredChannel)
                       ?? snapshot.Primary
                       ?? Installations.FirstOrDefault();

            if (snapshot.Error is { Length: > 0 })
            {
                StatusMessage = snapshot.Error;
                LogService.Warn(snapshot.Error);
            }
            else if (Selected is null)
            {
                StatusMessage = "No se encontró Microsoft.MinecraftUWP ni Minecraft Preview en este equipo.";
            }
            else
            {
                StatusMessage = $"{Selected.DisplayName} {Selected.Version} detectado.";
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error actualizando el estado del juego.");
            StatusMessage = "No se pudo detectar Minecraft: " + ex.Message;
        }
        finally
        {
            IsRefreshing = false;
            NotifyDerived();
            RefreshUpdateInfo();
        }
    }

    public void RefreshUpdateInfo()
    {
        Update = _game.ComputeUpdate(Selected, _catalog.Product);
        OnPropertyChanged(nameof(LastCheckLabel));
    }

    private async Task LaunchAsync()
    {
        if (Selected is null)
        {
            _dialogs.ShowMessage("Minecraft no instalado",
                "No se encontró una instalación oficial de Minecraft Bedrock. Instálalo desde Microsoft Store para continuar.",
                DialogKind.Info);
            return;
        }

        IsLaunching = true;
        try
        {
            if (_settings.Settings.VerifyBeforeLaunch)
            {
                var report = await _game.VerifyAsync(Selected);
                _dialogs.ShowVerification(report);
                if (report.HasErrors && !_dialogs.Confirm(
                        "La verificación encontró problemas",
                        "Windows informa de componentes que requieren atención. Puedes abrir Microsoft Store para repararlos o intentar iniciar el juego de todos modos.",
                        "Iniciar de todos modos", "Cancelar"))
                {
                    return;
                }
            }

            var result = await _game.LaunchAsync(Selected);
            if (!result.Success)
            {
                _dialogs.ShowError("No se pudo iniciar Minecraft", result.Message);
                return;
            }

            ToastHub.Success(result.Message);
            Launched?.Invoke(result);

            await Task.Delay(1200);
            await RefreshAsync();
        }
        finally
        {
            IsLaunching = false;
            NotifyDerived();
        }
    }

    private async Task VerifyAsync()
    {
        if (Selected is null)
            return;

        var report = await _game.VerifyAsync(Selected);
        _dialogs.ShowVerification(report);
        await RefreshAsync();
    }

    private void OpenInstallFolder()
    {
        if (Selected is null)
            return;

        if (!string.IsNullOrWhiteSpace(Selected.InstallLocation) && Directory.Exists(Selected.InstallLocation))
        {
            FileOps.OpenFolder(Selected.InstallLocation);
            return;
        }

        FileOps.OpenUrl("shell:AppsFolder");
        ToastHub.Info("Windows protege la carpeta del paquete. Se abrió la lista de aplicaciones instaladas.");
    }

    private void OpenUserDataFolder()
    {
        var path = Selected is null
            ? null
            : Paths.GetComMojangPath(Selected.FamilyName);

        if (path is null)
        {
            var expected = Selected is null
                ? Paths.GetComMojangPathOrExpected(Paths.RetailPackageFamily)
                : Paths.GetComMojangPathOrExpected(Selected.FamilyName);
            FileOps.OpenFolder(expected);
            ToastHub.Info("La carpeta de datos se crea la primera vez que Minecraft guarda contenido.");
            return;
        }

        FileOps.OpenFolder(path);
    }

    private void OnError(Exception exception)
    {
        LogService.Error(exception, "Error en una acción del juego.");
        _dialogs.ShowError("Error inesperado", exception.Message);
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(HasInstallation));
        OnPropertyChanged(nameof(IsGameRunning));
        OnPropertyChanged(nameof(HasMultipleChannels));
        OnPropertyChanged(nameof(HasMissingComponents));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(IsUpToDate));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(VersionLabel));
        OnPropertyChanged(nameof(ChannelLabel));
        OnPropertyChanged(nameof(InstallDateLabel));
        OnPropertyChanged(nameof(PackageStatusLabel));
        OnPropertyChanged(nameof(StatusHeadline));
        OnPropertyChanged(nameof(StatusDetail));
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayGlyph));
        OnPropertyChanged(nameof(MissingComponentsCount));
        OnPropertyChanged(nameof(ComponentsSummary));
        LaunchCommand.RaiseCanExecuteChanged();
        VerifyCommand.RaiseCanExecuteChanged();
    }
}
