using System.Collections.ObjectModel;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Official Store metadata + news feed state.</summary>
public sealed class CatalogState : ObservableObject
{
    private readonly StoreCatalogService _catalog;
    private StoreProduct? _product;
    private bool _isLoading;
    private bool _isOffline;
    private string _statusMessage = "Consultando Microsoft Store…";
    private bool _hasLoaded;

    public CatalogState(StoreCatalogService catalog)
    {
        _catalog = catalog;
        News = [];
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(force: true),
            onError: ex => ToastHub.Error("No se pudieron cargar las novedades."));
    }

    public StoreProduct? Product
    {
        get => _product;
        private set
        {
            if (SetProperty(ref _product, value))
            {
                OnPropertyChanged(nameof(HasProduct));
                OnPropertyChanged(nameof(Changelog));
                OnPropertyChanged(nameof(LastUpdatedLabel));
            }
        }
    }

    public ObservableCollection<NewsItem> News { get; }

    public bool HasProduct => Product is not null;
    public bool HasLoaded => _hasLoaded;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsOffline
    {
        get => _isOffline;
        private set => SetProperty(ref _isOffline, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string Changelog => Product?.Notes ?? "No hay notas de versión disponibles.";
    public string LastUpdatedLabel => Product?.LastUpdated is { } date
        ? $"Catálogo actualizado el {date.ToLocalTime():dd/MM/yyyy HH:mm}"
        : "Catálogo no disponible";

    public AsyncRelayCommand RefreshCommand { get; }
    public event Action? Updated;

    public async Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusMessage = "Consultando Microsoft Store…";
        try
        {
            var product = await _catalog.GetProductAsync(force, cancellationToken);
            Product = product;
            IsOffline = product is null;

            if (product is not null)
            {
                StatusMessage = "Catálogo oficial sincronizado.";
                Updated?.Invoke();

                var news = await _catalog.GetNewsAsync(product, cancellationToken);
                News.Clear();
                foreach (var item in news)
                    News.Add(item);

                if (News.Count == 0)
                {
                    var fallback = new NewsItem
                    {
                        Title = "Novedades no disponibles",
                        Summary = "No se pudo cargar el feed. Comprueba tu conexión a Internet.",
                        Category = "Aviso",
                        ImagePath = Paths.GetBuiltInBackground("cueva"),
                        Url = product.StoreUrl,
                        IsOfficial = true
                    };
                    News.Add(fallback);
                }
            }
            else
            {
                StatusMessage = "Sin conexión con Microsoft Store.";
                Updated?.Invoke();
                var news = await _catalog.GetNewsAsync(null, cancellationToken);
                News.Clear();
                foreach (var item in news)
                    News.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Consulta cancelada.";
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error actualizando el catálogo.");
            IsOffline = true;
            StatusMessage = "No se pudo consultar el catálogo.";
            ToastHub.Warning("No se pudo consultar Microsoft Store. Se muestra la información local.");
        }
        finally
        {
            _hasLoaded = true;
            IsLoading = false;
            OnPropertyChanged(nameof(HasLoaded));
        }
    }
}
