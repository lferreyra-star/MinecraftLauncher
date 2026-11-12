using XdLauncher.Core;

namespace XdLauncher.Models;

public enum InstallationChannel
{
    Retail,
    Preview
}

public sealed class GameDependency
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public bool IsFramework { get; init; }
}

public enum ComponentState
{
    Installed,
    Missing,
    Unknown
}

public sealed class ComponentCheck
{
    public required string Name { get; init; }
    public required string PackageName { get; init; }
    public required string Description { get; init; }
    public ComponentState State { get; init; }
    public string? Version { get; init; }
    public bool IsRequired { get; init; } = true;
    public string? StoreProductId { get; init; }
    public string IconGlyph { get; init; } = "\uE73E";

    public bool IsInstalled => State == ComponentState.Installed;
    public bool CanInstall => !IsInstalled && !string.IsNullOrWhiteSpace(StoreProductId);
    public string StateLabel => State switch
    {
        ComponentState.Installed => "Instalado",
        ComponentState.Missing => "Falta",
        _ => "No verificable"
    };
}

public sealed class GameInstallation
{
    public required InstallationChannel Channel { get; init; }
    public required string DisplayName { get; init; }
    public required string PackageName { get; init; }
    public required string PackageFullName { get; init; }
    public required string FamilyName { get; init; }
    public required string Version { get; init; }
    public required string Architecture { get; init; }
    public required string Publisher { get; init; }
    public required DateTimeOffset InstalledDate { get; init; }
    public required string InstallLocation { get; init; }
    public required string AppUserModelId { get; init; }
    public required bool IsHealthy { get; init; }
    public required string PackageStatus { get; init; }
    public required string SignatureKind { get; init; }
    public List<GameDependency> Dependencies { get; init; } = [];
    public List<ComponentCheck> Components { get; init; } = [];
    public bool IsRunning { get; set; }

    public bool HasMissingComponents => Components.Any(c => c.IsRequired && c.State == ComponentState.Missing);
    public int MissingRequiredCount => Components.Count(c => c.IsRequired && c.State == ComponentState.Missing);
    public string ChannelLabel => Channel == InstallationChannel.Preview ? "Preview" : "Retail";
    public string Aumid => !string.IsNullOrWhiteSpace(AppUserModelId)
        ? AppUserModelId
        : $"{FamilyName}!App";
    public string StatusLabel => !IsHealthy ? "Requiere atención" : IsRunning ? "En ejecución" : "Listo para jugar";
}

public enum VerificationState
{
    Ok,
    Warning,
    Error,
    Unknown
}

public sealed class VerificationItem
{
    public required string Name { get; init; }
    public required VerificationState State { get; init; }
    public required string Detail { get; init; }
}

public sealed class VerificationReport
{
    public List<VerificationItem> Items { get; init; } = [];
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;

    public bool HasErrors => Items.Any(i => i.State == VerificationState.Error);
    public bool HasWarnings => Items.Any(i => i.State == VerificationState.Warning);
    public string Summary => HasErrors
        ? "Se encontraron problemas"
        : HasWarnings ? "Correcto con advertencias" : "Todo correcto";
}

public enum UpdateAvailability
{
    Unknown,
    UpToDate,
    Available
}

public sealed class UpdateInfo
{
    public UpdateAvailability Availability { get; init; } = UpdateAvailability.Unknown;
    public DateTimeOffset? StoreUpdatedAt { get; init; }
    public DateTimeOffset? LocalInstalledAt { get; init; }
    public string? Notes { get; init; }
    public string? StoreUrl { get; init; }
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;

    public string StatusText => Availability switch
    {
        UpdateAvailability.Available => "Actualización disponible",
        UpdateAvailability.UpToDate => "Al día",
        _ => "Sin verificar"
    };

    public string Detail => Availability switch
    {
        UpdateAvailability.Available =>
            $"Microsoft Store publicó cambios el {StoreUpdatedAt:dd/MM/yyyy}. Tu instalación es del {LocalInstalledAt:dd/MM/yyyy}.",
        UpdateAvailability.UpToDate =>
            "Tu instalación está al día según el catálogo de Microsoft Store.",
        _ => "Abre Microsoft Store para comprobar actualizaciones oficiales."
    };
}

public sealed class PlayerProfile
{
    public required string Gamertag { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Gamerscore { get; init; }
    public string? Xuid { get; init; }
    public bool IsMicrosoftAccount { get; init; }
    public bool IsLocal { get; init; }
    public string? AvatarPath { get; set; }
    public string AccountTypeLabel => IsMicrosoftAccount ? "Cuenta Microsoft · Xbox Live" : "Perfil local del launcher";
    public string Initial => string.IsNullOrWhiteSpace(Gamertag) ? "?" : Gamertag[..1].ToUpperInvariant();
}

public sealed class NewsItem
{
    public required string Title { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Category { get; init; } = "Novedades";
    public DateTimeOffset? Date { get; init; }
    public string? ImagePath { get; set; }
    public string? Url { get; init; }
    public bool IsOfficial { get; init; } = true;
    public string DateLabel => Date?.ToLocalTime().ToString("dd MMM yyyy") ?? "Reciente";
}

public sealed class StoreProduct
{
    public string Title { get; init; } = "Minecraft for Windows";
    public string ShortDescription { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public DateTimeOffset? LastUpdated { get; init; }
    public List<string> ImageUrls { get; init; } = [];
    public string? HeroImageUrl { get; init; }
    public string? LogoImageUrl { get; init; }
    public string StoreUrl { get; init; } = "https://apps.microsoft.com/detail/9NBLGGH2JHXJ";
    public string? ProductId { get; init; } = "9NBLGGH2JHXJ";
    public List<string> FrameworkDependencies { get; init; } = [];
}

public enum AddonCategory
{
    All,
    Addon,
    ResourcePack,
    BehaviorPack,
    SkinPack,
    Map,
    WorldTemplate
}

public sealed class AddonItem : ObservableObject
{
    private string? _iconPath;

    public required string Key { get; init; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public AddonCategory Category { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string Uuid { get; init; } = string.Empty;
    public string? FolderPath { get; init; }
    public string? SourceFile { get; init; }
    public string? ManifestPath { get; init; }
    public bool IsInstalled { get; init; }
    public bool IsWorld { get; init; }
    public string? ExtraInfo { get; init; }
    public string? GameChannel { get; init; }
    public List<string> ModuleTypes { get; init; } = [];

    public string? IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    public string CategoryLabel => Category switch
    {
        AddonCategory.Addon => "Addon",
        AddonCategory.ResourcePack => "Texturas",
        AddonCategory.BehaviorPack => "Comportamiento",
        AddonCategory.SkinPack => "Skins",
        AddonCategory.Map => "Mapa",
        AddonCategory.WorldTemplate => "Plantilla",
        _ => "Contenido"
    };

    public string OriginLabel => IsInstalled ? "Instalado" : "Biblioteca local";
}

public sealed class AddonImportResult
{
    public required string Name { get; init; }
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public string? Destination { get; init; }
    public AddonCategory Category { get; init; }
}

public sealed class ToastMessage
{
    public required string Message { get; init; }
    public string IconGlyph { get; init; } = "\uE73E";
    public bool IsError { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

public enum DownloadState
{
    Idle,
    Queued,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled,
    ReadyToInstall,
    Installed
}

public sealed class DownloadItem : ObservableObject
{
    private long _bytesReceived;
    private long? _totalBytes;
    private string _statusMessage = "Listo para descargar";
    private double _progressPercent;
    private bool _isIndeterminate;

    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string DownloadUrl { get; init; }
    public required string StoreUrl { get; init; }
    public required string FileName { get; init; }
    public required string Category { get; init; }
    public required string IconGlyph { get; init; }
    public long SizeBytes { get; init; }
    public string SizeLabel { get; init; } = string.Empty;
    private DownloadState _state = DownloadState.Idle;
    public DownloadState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(StateLabel));
            }
        }
    }
    public bool CanDownload => State is DownloadState.Idle or DownloadState.Failed or DownloadState.Cancelled;
    public bool CanCancel => State == DownloadState.Downloading || State == DownloadState.Queued;
    public bool CanOpenStore => !string.IsNullOrWhiteSpace(StoreUrl);
    public bool IsMinecraft => Category.Equals("Minecraft", StringComparison.OrdinalIgnoreCase);
    public string CategoryLabel => Category;

    public long BytesReceived
    {
        get => _bytesReceived;
        private set => SetProperty(ref _bytesReceived, value);
    }

    public long? TotalBytes
    {
        get => _totalBytes;
        private set => SetProperty(ref _totalBytes, value);
    }

    public double? Percent => TotalBytes is > 0
        ? Math.Clamp(BytesReceived * 100d / TotalBytes.Value, 0, 100)
        : null;

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        internal set => SetProperty(ref _isIndeterminate, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        internal set => SetProperty(ref _statusMessage, value);
    }

    public string ProgressText
    {
        get
        {
            if (TotalBytes is { } total && total > 0)
            {
                var mbReceived = BytesReceived / (1024d * 1024);
                var mbTotal = total / (1024d * 1024);
                var pct = Percent is { } p ? p : 0;
                return $"{mbReceived:F1} MB / {mbTotal:F1} MB · {pct:F0}%";
            }
            if (BytesReceived > 0)
            {
                var mb = BytesReceived / (1024d * 1024);
                return $"{mb:F1} MB descargado";
            }
            return "Esperando…";
        }
    }

    public string StateLabel => State switch
    {
        DownloadState.Idle => "Listo",
        DownloadState.Queued => "En cola",
        DownloadState.Downloading => "Descargando",
        DownloadState.Paused => "Pausado",
        DownloadState.Completed => "Descargado",
        DownloadState.Failed => "Error",
        DownloadState.Cancelled => "Cancelado",
        DownloadState.ReadyToInstall => "Listo para instalar",
        DownloadState.Installed => "Instalado",
        _ => "Desconocido"
    };

    public void SetState(DownloadState state)
    {
        State = state;
        OnPropertyChanged(nameof(StatusMessage));
    }

    public void UpdateProgress(long bytesReceived, long? totalBytes)
    {
        BytesReceived = bytesReceived;
        TotalBytes = totalBytes;
        ProgressPercent = Percent is { } p ? p : 0;
        IsIndeterminate = totalBytes is null or <= 0;
        if (State != DownloadState.Downloading)
            SetState(DownloadState.Downloading);
        StatusMessage = $"Descargando {Title}…";
        OnPropertyChanged(nameof(ProgressText));
    }

    public void MarkCompleted()
    {
        SetState(DownloadState.Completed);
        ProgressPercent = 100;
        IsIndeterminate = false;
        StatusMessage = "Descarga completada.";
        OnPropertyChanged(nameof(ProgressText));
    }

    public void MarkFailed(string reason)
    {
        SetState(DownloadState.Failed);
        StatusMessage = reason;
        IsIndeterminate = false;
        OnPropertyChanged(nameof(ProgressText));
    }

    public void MarkCancelled()
    {
        SetState(DownloadState.Cancelled);
        StatusMessage = "Descarga cancelada.";
        IsIndeterminate = false;
        OnPropertyChanged(nameof(ProgressText));
    }

    public void MarkReadyToInstall()
    {
        SetState(DownloadState.ReadyToInstall);
        StatusMessage = "Listo para instalar.";
        OnPropertyChanged(nameof(ProgressText));
    }

    public void MarkInstalled()
    {
        SetState(DownloadState.Installed);
        StatusMessage = "Instalado.";
        OnPropertyChanged(nameof(ProgressText));
    }

    public void Reset()
    {
        BytesReceived = 0;
        TotalBytes = null;
        ProgressPercent = 0;
        IsIndeterminate = false;
        SetState(DownloadState.Idle);
        StatusMessage = "Listo para descargar";
        OnPropertyChanged(nameof(ProgressText));
    }
}
