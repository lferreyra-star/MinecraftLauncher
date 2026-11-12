using System.Collections.ObjectModel;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>Interfaz de descarga: gestiona descargas de Minecraft y herramientas del launcher.</summary>
public sealed class DownloadViewModel : ObservableObject
{
    private readonly MinecraftDownloadService _downloads;
    private readonly DialogService _dialogs;
    private string _searchText = string.Empty;
    private string _filterCategory = "Todo";
    private bool _isRefreshing;

    public DownloadViewModel(MinecraftDownloadService downloads, DialogService dialogs)
    {
        _downloads = downloads;
        _dialogs = dialogs;

        Categories = ["Todo", "Minecraft", "Herramientas"];

        startDownloadCommand = new AsyncRelayCommand<DownloadItem>(StartDownloadAsync,
            item => item is not null && item.CanDownload, OnError);
        cancelDownloadCommand = new AsyncRelayCommand<DownloadItem>(CancelDownloadAsync,
            item => item is not null && item.CanCancel, OnError);
        retryDownloadCommand = new RelayCommand<DownloadItem>(RetryDownload,
            item => item is not null && item.State is DownloadState.Failed or DownloadState.Cancelled);
        openStoreCommand = new RelayCommand<DownloadItem>(OpenStore,
            item => item is not null && item.CanOpenStore);
        openFileCommand = new RelayCommand<DownloadItem>(OpenFile,
            item => item is not null && item.State is DownloadState.Completed or DownloadState.ReadyToInstall);
        refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !_isRefreshing, OnError);
        cancelAllCommand = new RelayCommand(CancelAll, () => _downloads.IsAnyDownloading);
    }

    public ObservableCollection<DownloadItem> Items { get; } = [];
    public ObservableCollection<DownloadItem> VisibleItems { get; } = [];
    public IReadOnlyList<string> Categories { get; }

    private readonly AsyncRelayCommand<DownloadItem>? startDownloadCommand;
    private readonly AsyncRelayCommand<DownloadItem>? cancelDownloadCommand;
    private readonly RelayCommand<DownloadItem>? retryDownloadCommand;
    private readonly RelayCommand<DownloadItem>? openStoreCommand;
    private readonly RelayCommand<DownloadItem>? openFileCommand;
    private readonly AsyncRelayCommand? refreshCommand;
    private readonly RelayCommand? cancelAllCommand;

    public AsyncRelayCommand<DownloadItem> StartDownloadCommand => startDownloadCommand!;
    public AsyncRelayCommand<DownloadItem> CancelDownloadCommand => cancelDownloadCommand!;
    public RelayCommand<DownloadItem> RetryDownloadCommand => retryDownloadCommand!;
    public RelayCommand<DownloadItem> OpenStoreCommand => openStoreCommand!;
    public RelayCommand<DownloadItem> OpenFileCommand => openFileCommand!;
    public AsyncRelayCommand RefreshCommand => refreshCommand!;
    public RelayCommand CancelAllCommand => cancelAllCommand!;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
                ApplyFilter();
        }
    }

    public string FilterCategory
    {
        get => _filterCategory;
        set
        {
            if (SetProperty(ref _filterCategory, value))
                ApplyFilter();
        }
    }

    public bool IsAnyDownloading => _downloads.IsAnyDownloading;
    public int ActiveDownloads => Items.Count(i => i.State == DownloadState.Downloading);
    public int CompletedDownloads => Items.Count(i => i.State is DownloadState.Completed or DownloadState.Installed);
    public int FailedDownloads => Items.Count(i => i.State == DownloadState.Failed);
    public int TotalCount => Items.Count;
    public bool HasItems => VisibleItems.Count > 0;

    public async Task EnsureLoadedAsync()
    {
        if (Items.Count == 0)
            await RefreshAsync();
    }

    public Task RefreshAsync()
    {
        if (_isRefreshing)
            return Task.CompletedTask;

        _isRefreshing = true;
        RefreshCommand.RaiseCanExecuteChanged();
        try
        {
            if (Items.Count == 0)
            {
                foreach (var item in _downloads.Items)
                {
                    Items.Add(item);
                    item.PropertyChanged += OnItemChanged;
                }
            }

            ApplyFilter();
            NotifyStats();
        }
        finally
        {
            _isRefreshing = false;
            RefreshCommand.RaiseCanExecuteChanged();
        }
        return Task.CompletedTask;
    }

    private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DownloadItem.State))
        {
            NotifyStats();
            CancelAllCommand.RaiseCanExecuteChanged();
            ReapplyCommands();
        }
    }

    private async Task StartDownloadAsync(DownloadItem? item)
    {
        if (item is null)
            return;
        await _downloads.StartDownloadAsync(item);
        NotifyStats();
    }

    private async Task CancelDownloadAsync(DownloadItem? item)
    {
        if (item is null)
            return;
        await _downloads.CancelDownloadAsync(item);
        NotifyStats();
    }

    private void RetryDownload(DownloadItem? item)
    {
        if (item is null)
            return;
        _downloads.RetryDownload(item);
        NotifyStats();
    }

    private void OpenStore(DownloadItem? item)
    {
        if (item is not null)
            _downloads.OpenStore(item);
    }

    private void OpenFile(DownloadItem? item)
    {
        if (item is not null)
            _downloads.OpenDownloadedFile(item);
    }

    private void CancelAll()
    {
        foreach (var item in Items.Where(i => i.CanCancel).ToList())
            _ = CancelDownloadAsync(item);
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var category = FilterCategory;

        var filtered = Items.Where(item =>
        {
            if (category != "Todo" && item.Category != category)
                return false;
            if (query.Length == 0)
                return true;
            return item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                   item.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }).ToList();

        VisibleItems.Clear();
        foreach (var item in filtered)
            VisibleItems.Add(item);

        OnPropertyChanged(nameof(HasItems));
        NotifyStats();
    }

    private void NotifyStats()
    {
        OnPropertyChanged(nameof(ActiveDownloads));
        OnPropertyChanged(nameof(CompletedDownloads));
        OnPropertyChanged(nameof(FailedDownloads));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(IsAnyDownloading));
        OnPropertyChanged(nameof(HasItems));
    }

    private void ReapplyCommands()
    {
        StartDownloadCommand.RaiseCanExecuteChanged();
        CancelDownloadCommand.RaiseCanExecuteChanged();
        RetryDownloadCommand.RaiseCanExecuteChanged();
        OpenStoreCommand.RaiseCanExecuteChanged();
        OpenFileCommand.RaiseCanExecuteChanged();
    }

    private void OnError(Exception exception)
    {
        LogService.Error(exception, "Error en la interfaz de descargas.");
        _dialogs.ShowError("Error de descargas", exception.Message);
    }
}
