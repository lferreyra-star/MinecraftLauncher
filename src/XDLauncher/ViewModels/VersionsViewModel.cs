using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Installed version, official Store update status and changelog.</summary>
public sealed class VersionsViewModel : ObservableObject
{
    private readonly GameState _game;
    private readonly CatalogState _catalog;
    private bool _isChecking;

    public VersionsViewModel(GameState game, CatalogState catalog)
    {
        _game = game;
        _catalog = catalog;

        CheckCommand = new AsyncRelayCommand(CheckAsync, () => !IsChecking, OnError);
        OpenStoreCommand = new RelayCommand(() => _game.OpenStoreCommand.Execute(null));
        OpenUpdatesCommand = new RelayCommand(() => _game.OpenUpdatesCommand.Execute(null));
        CopyVersionCommand = new RelayCommand(() =>
        {
            if (HasInstallation)
                FileOps.CopyText(Version);
        });
        OpenFolderCommand = new RelayCommand(() =>
        {
            var path = _game.Selected is null
                ? Paths.GetComMojangPathOrExpected(Paths.RetailPackageFamily)
                : Paths.GetComMojangPathOrExpected(_game.Selected.FamilyName);
            FileOps.OpenFolder(path);
        });

        _game.PropertyChanged += (_, _) => NotifyAll();
        _catalog.PropertyChanged += (_, _) => NotifyAll();
        _catalog.Updated += NotifyAll;
    }

    public bool HasInstallation => _game.IsInstalled;
    public string Version => _game.VersionLabel;
    public string Channel => _game.ChannelLabel;
    public string DisplayName => _game.DisplayName;
    public string InstalledDate => _game.InstallDateLabel;
    public string PackageFullName => _game.Selected?.PackageFullName ?? "—";
    public string Publisher => _game.Selected?.Publisher ?? "—";
    public string Architecture => _game.Selected?.Architecture ?? "—";
    public string SignatureKind => _game.Selected?.SignatureKind ?? "—";
    public string PackageStatus => _game.PackageStatusLabel;
    public string UpdateStatus => _game.UpdateStatus;
    public string UpdateDetail => _game.UpdateDetail;
    public bool HasUpdate => _game.HasUpdate;
    public bool IsUpToDate => _game.IsUpToDate;
    public string StoreUpdatedLabel => _catalog.LastUpdatedLabel;
    public string Changelog => _catalog.Changelog;
    public string CatalogStatus => _catalog.StatusMessage;
    public bool IsCatalogOffline => _catalog.IsOffline;
    public bool IsChecking
    {
        get => _isChecking;
        private set
        {
            if (SetProperty(ref _isChecking, value))
            {
                OnPropertyChanged(nameof(CheckLabel));
                CheckCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CheckLabel => IsChecking ? "Comprobando…" : "Buscar actualizaciones";
    public string UpdateHeadline => HasUpdate
        ? "Hay una actualización oficial disponible"
        : IsUpToDate
            ? "Tu instalación está al día"
            : HasInstallation
                ? "Comprueba si hay una versión nueva"
                : "Instala Minecraft para gestionar versiones";

    public string UpdateDescription => HasUpdate
        ? "Microsoft Store publicó cambios después de tu fecha de instalación. Abre Store para actualizar por el canal oficial."
        : IsUpToDate
            ? "La fecha del catálogo oficial no es posterior a tu instalación. No se requieren acciones."
            : "El launcher compara la fecha del catálogo oficial de Microsoft Store con la fecha de instalación registrada por Windows. Nunca instala versiones antiguas ni modificadas.";

    public AsyncRelayCommand CheckCommand { get; }
    public RelayCommand OpenStoreCommand { get; }
    public RelayCommand OpenUpdatesCommand { get; }
    public RelayCommand CopyVersionCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    private async Task CheckAsync()
    {
        IsChecking = true;
        try
        {
            await _catalog.RefreshAsync(force: true);
            await _game.RefreshAsync();
            _game.RefreshUpdateInfo();
            NotifyAll();

            if (!_game.IsInstalled)
                ToastHub.Info("Catálogo consultado. Minecraft no está instalado en este equipo.");
            else if (_game.HasUpdate)
                ToastHub.Warning("Hay una actualización disponible en Microsoft Store.");
            else if (_game.IsUpToDate)
                ToastHub.Success("Minecraft está al día según Microsoft Store.");
            else
                ToastHub.Info("No se pudo confirmar el estado de actualizaciones.");
        }
        finally
        {
            IsChecking = false;
        }
    }

    public void NotifyVersionState() => NotifyAll();

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(HasInstallation));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(Channel));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(InstalledDate));
        OnPropertyChanged(nameof(PackageFullName));
        OnPropertyChanged(nameof(Publisher));
        OnPropertyChanged(nameof(Architecture));
        OnPropertyChanged(nameof(SignatureKind));
        OnPropertyChanged(nameof(PackageStatus));
        OnPropertyChanged(nameof(UpdateStatus));
        OnPropertyChanged(nameof(UpdateDetail));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(IsUpToDate));
        OnPropertyChanged(nameof(StoreUpdatedLabel));
        OnPropertyChanged(nameof(Changelog));
        OnPropertyChanged(nameof(CatalogStatus));
        OnPropertyChanged(nameof(IsCatalogOffline));
        OnPropertyChanged(nameof(UpdateHeadline));
        OnPropertyChanged(nameof(UpdateDescription));
    }

    private void OnError(Exception exception)
    {
        LogService.Error(exception, "Error comprobando actualizaciones.");
        ToastHub.Error("No se pudieron comprobar las actualizaciones.");
    }
}
