using System.Collections.ObjectModel;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

public sealed class AddonCategoryOption
{
    public required string Label { get; init; }
    public required AddonCategory Category { get; init; }
}

/// <summary>Installed add-on explorer and safe importer state.</summary>
public sealed class AddonsViewModel : ObservableObject
{
    private readonly AddonService _addons;
    private readonly GameState _game;
    private readonly SettingsState _settings;
    private readonly DialogService _dialogs;

    private bool _isBusy;
    private string _searchText = string.Empty;
    private AddonCategoryOption? _selectedCategory;
    private string _statusMessage = "Explorando complementos instalados…";
    private bool _hasLoaded;

    public AddonsViewModel(AddonService addons, GameState game, SettingsState settings, DialogService dialogs)
    {
        _addons = addons;
        _game = game;
        _settings = settings;
        _dialogs = dialogs;

        Items = [];
        VisibleItems = [];
        Categories =
        [
            new AddonCategoryOption { Label = "Todo el contenido", Category = AddonCategory.All },
            new AddonCategoryOption { Label = "Addons y paquetes", Category = AddonCategory.Addon },
            new AddonCategoryOption { Label = "Texturas", Category = AddonCategory.ResourcePack },
            new AddonCategoryOption { Label = "Comportamiento", Category = AddonCategory.BehaviorPack },
            new AddonCategoryOption { Label = "Skins", Category = AddonCategory.SkinPack },
            new AddonCategoryOption { Label = "Mapas", Category = AddonCategory.Map },
            new AddonCategoryOption { Label = "Plantillas de mundo", Category = AddonCategory.WorldTemplate }
        ];
        _selectedCategory = Categories[0];

        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(force: true), onError: OnError);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => !IsBusy, OnError);
        AddToLibraryCommand = new AsyncRelayCommand(AddToLibraryAsync, () => !IsBusy, OnError);
        InstallItemCommand = new AsyncRelayCommand(InstallItemAsync,
            parameter => !IsBusy && parameter is AddonItem, OnError);
        DeleteItemCommand = new AsyncRelayCommand(DeleteItemAsync,
            parameter => !IsBusy && parameter is AddonItem, OnError);
        OpenItemCommand = new RelayCommand(OpenItem);
        OpenLibraryCommand = new RelayCommand(() => FileOps.OpenFolder(Paths.LibraryDirectory));
        OpenGameFolderCommand = new RelayCommand(OpenGameFolder);
    }

    public ObservableCollection<AddonItem> Items { get; }
    public ObservableCollection<AddonItem> VisibleItems { get; }
    public IReadOnlyList<AddonCategoryOption> Categories { get; }

    public AddonCategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                ApplyFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
                ApplyFilter();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(StatusMessage));
                ImportCommand.RaiseCanExecuteChanged();
                AddToLibraryCommand.RaiseCanExecuteChanged();
                InstallItemCommand.RaiseCanExecuteChanged();
                DeleteItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasLoaded => _hasLoaded;
    public bool HasItems => VisibleItems.Count > 0;
    public bool IsEmpty => HasLoaded && VisibleItems.Count == 0;
    public string CountLabel => Items.Count == 0
        ? "Sin complementos"
        : $"{Items.Count} elementos · {Items.Count(i => i.IsInstalled)} instalados";
    public string FilterSummary => string.IsNullOrWhiteSpace(SearchText) &&
                                    SelectedCategory?.Category == AddonCategory.All
        ? "Mostrando todo el contenido disponible"
        : $"{VisibleItems.Count} resultados";

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public AsyncRelayCommand AddToLibraryCommand { get; }
    public AsyncRelayCommand InstallItemCommand { get; }
    public AsyncRelayCommand DeleteItemCommand { get; }
    public RelayCommand OpenItemCommand { get; }
    public RelayCommand OpenLibraryCommand { get; }
    public RelayCommand OpenGameFolderCommand { get; }

    public async Task EnsureLoadedAsync()
    {
        if (_hasLoaded || IsBusy)
            return;
        await RefreshAsync(force: false);
    }

    public async Task RefreshAsync(bool force = false)
    {
        if (IsBusy)
            return;
        if (_hasLoaded && !force)
            return;

        IsBusy = true;
        StatusMessage = "Explorando com.mojang y la biblioteca local…";
        try
        {
            var (installed, library) = await Task.Run(() =>
            {
                var installedItems = _addons.ScanInstalled();
                var libraryItems = _addons.ScanLibrary();
                return (installedItems, libraryItems);
            });

            Items.Clear();
            foreach (var item in installed
                         .OrderByDescending(i => i.IsInstalled)
                         .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
                Items.Add(item);
            foreach (var item in library)
                Items.Add(item);

            _hasLoaded = true;
            ApplyFilter();

            var channel = GetChannel();
            var root = Paths.GetComMojangPathOrExpected(channel == InstallationChannel.Preview
                ? Paths.PreviewPackageFamily
                : Paths.RetailPackageFamily);

            StatusMessage = Directory.Exists(root)
                ? $"Carpeta del juego: {root}"
                : "Minecraft aún no ha creado su carpeta de contenido. Se creará al importar el primer complemento.";
            OnPropertyChanged(nameof(HasLoaded));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(CountLabel));
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo explorar la biblioteca de complementos.");
            StatusMessage = "No se pudieron leer todos los complementos.";
            ToastHub.Error("No se pudo explorar la carpeta de complementos.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ImportFilesAsync(IEnumerable<string> files)
    {
        if (IsBusy)
            return;

        var list = files.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0)
            return;

        IsBusy = true;
        StatusMessage = "Importando complementos…";
        var results = new List<AddonImportResult>();
        try
        {
            foreach (var file in list)
            {
                var imported = await _addons.ImportFileAsync(file, GetChannel());
                results.AddRange(imported);
                StatusMessage = $"Procesado {System.IO.Path.GetFileName(file)}…";
            }

            _dialogs.ShowImportResults(results);
            var ok = results.Count(r => r.Success);
            if (ok > 0)
                ToastHub.Success($"{ok} complemento(s) importado(s). Abre Minecraft para disfrutarlos.");
            if (ok < results.Count)
                ToastHub.Warning("Algunos archivos no se pudieron importar.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error importando complementos.");
            _dialogs.ShowError("Error al importar", ex.Message);
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync(force: true);
        }
    }

    private async Task ImportAsync()
    {
        var files = _dialogs.PickAddonFiles();
        if (files is null || files.Count == 0)
            return;
        await ImportFilesAsync(files);
    }

    private async Task AddToLibraryAsync()
    {
        var files = _dialogs.PickAddonFiles();
        if (files is null || files.Count == 0)
            return;

        IsBusy = true;
        var copied = 0;
        try
        {
            Directory.CreateDirectory(Paths.LibraryDirectory);
            foreach (var file in files)
            {
                try
                {
                    var extension = System.IO.Path.GetExtension(file);
                    if (!_addons.SupportedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        ToastHub.Warning($"{System.IO.Path.GetFileName(file)} no es un formato compatible.");
                        continue;
                    }

                    var destination = GetLibraryDestination(file);
                    File.Copy(file, destination, overwrite: false);
                    copied++;
                }
                catch (Exception ex)
                {
                    LogService.Warn($"No se pudo copiar '{file}' a la biblioteca: {ex.Message}");
                }
            }

            if (copied > 0)
                ToastHub.Success($"{copied} archivo(s) añadido(s) a la biblioteca local.");
            else
                ToastHub.Warning("No se añadió ningún archivo.");
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync(force: true);
        }
    }

    private async Task InstallItemAsync(object? parameter)
    {
        if (parameter is not AddonItem { SourceFile: { Length: > 0 } file } item)
            return;
        if (!File.Exists(file))
        {
            ToastHub.Warning("El archivo original ya no existe en la biblioteca.");
            return;
        }

        IsBusy = true;
        try
        {
            var results = await _addons.ImportFileAsync(file, GetChannel());
            _dialogs.ShowImportResults(results);
            if (results.Any(r => r.Success))
                ToastHub.Success($"'{item.Name}' se instaló en Minecraft.");
            await RefreshAsync(force: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteItemAsync(object? parameter)
    {
        if (parameter is not AddonItem item)
            return;

        var target = item.IsInstalled ? item.FolderPath : item.SourceFile;
        if (string.IsNullOrWhiteSpace(target))
        {
            ToastHub.Warning("No se pudo determinar la ubicación del complemento.");
            return;
        }

        if (!_dialogs.Confirm("Eliminar complemento",
                $"'{item.Name}' se enviará a la Papelera de reciclaje. Minecraft dejará de verlo al reiniciar.",
                "Eliminar", danger: true))
            return;

        IsBusy = true;
        try
        {
            if (FileOps.MoveToRecycleBin(target))
                ToastHub.Success("Elemento enviado a la Papelera de reciclaje.");
            else
                ToastHub.Error("No se pudo eliminar el elemento.");

            await RefreshAsync(force: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenItem(object? parameter)
    {
        if (parameter is not AddonItem item)
            return;

        if (!string.IsNullOrWhiteSpace(item.FolderPath) && Directory.Exists(item.FolderPath))
            FileOps.OpenFolder(item.FolderPath);
        else if (!string.IsNullOrWhiteSpace(item.SourceFile))
            FileOps.ShowInExplorer(item.SourceFile);
    }

    private void OpenGameFolder()
    {
        var channel = GetChannel();
        var expected = Paths.GetComMojangPathOrExpected(channel == InstallationChannel.Preview
            ? Paths.PreviewPackageFamily
            : Paths.RetailPackageFamily);
        if (Directory.Exists(expected))
            FileOps.OpenFolder(expected);
        else
        {
            FileOps.OpenFolder(Paths.LibraryDirectory);
            ToastHub.Info("Minecraft todavía no creó su carpeta de contenido. Se abrió la biblioteca del launcher.");
        }
    }

    private InstallationChannel GetChannel()
    {
        if (_game.Selected is not null)
            return _game.Selected.Channel;
        return _settings.Settings.PreferredChannel == "Preview"
            ? InstallationChannel.Preview
            : InstallationChannel.Retail;
    }

    private static string GetLibraryDestination(string source)
    {
        var fileName = System.IO.Path.GetFileName(source);
        var destination = System.IO.Path.Combine(Paths.LibraryDirectory, fileName);
        if (!File.Exists(destination))
            return destination;

        var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var extension = System.IO.Path.GetExtension(fileName);
        return System.IO.Path.Combine(Paths.LibraryDirectory, $"{stem}-{DateTime.Now:yyyyMMddHHmmss}{extension}");
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var category = SelectedCategory?.Category ?? AddonCategory.All;

        var filtered = Items.Where(item =>
        {
            if (category != AddonCategory.All && item.Category != category)
                return false;
            if (query.Length == 0)
                return true;
            return item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                   item.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                   item.CategoryLabel.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }).ToList();

        VisibleItems.Clear();
        foreach (var item in filtered)
            VisibleItems.Add(item);

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(FilterSummary));
    }

    private void OnError(Exception exception)
    {
        LogService.Error(exception, "Error en una acción de complementos.");
        _dialogs.ShowError("Error de complementos", exception.Message);
    }
}
