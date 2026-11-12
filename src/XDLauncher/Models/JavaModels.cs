using XdLauncher.Core;

namespace XdLauncher.Models;

/// <summary>Entrada del manifiesto oficial de versiones de Minecraft Java.</summary>
public sealed class JavaVersionEntry : ObservableObject
{
    private bool _isInstalled;
    private bool _isBusy;
    private string _modLoaderLabel = "Vanilla";
    private string _profileTypeLabel = "—";
    private string _profilePlayerName = string.Empty;
    private bool _hasProfile;

    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Url { get; init; }
    public required DateTimeOffset ReleaseTime { get; init; }
    public string Sha1 { get; init; } = string.Empty;

    public string ModLoaderLabel
    {
        get => _modLoaderLabel;
        set
        {
            if (SetProperty(ref _modLoaderLabel, value))
                OnPropertyChanged(nameof(ProfileSummary));
        }
    }

    public string ProfileTypeLabel
    {
        get => _profileTypeLabel;
        set
        {
            if (SetProperty(ref _profileTypeLabel, value))
                OnPropertyChanged(nameof(ProfileSummary));
        }
    }

    public string ProfilePlayerName
    {
        get => _profilePlayerName;
        set
        {
            if (SetProperty(ref _profilePlayerName, value))
                OnPropertyChanged(nameof(ProfileSummary));
        }
    }

    public bool HasProfile
    {
        get => _hasProfile;
        set
        {
            if (SetProperty(ref _hasProfile, value))
                OnPropertyChanged(nameof(ProfileSummary));
        }
    }

    public string ProfileSummary => HasProfile
        ? $"{ProfileTypeLabel} · {ModLoaderLabel}" + (string.IsNullOrWhiteSpace(ProfilePlayerName)
            ? string.Empty
            : $" · {ProfilePlayerName}")
        : $"{ProfileTypeLabel} · {ModLoaderLabel}";

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (SetProperty(ref _isInstalled, value))
            {
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(CanPlay));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(StatusLabel));
            }
        }
    }

    public bool CanPlay => IsInstalled && !IsBusy;

    public string TypeLabel => Type switch
    {
        "release" => "Release",
        "snapshot" => "Snapshot",
        "old_beta" => "Beta antigua",
        "old_alpha" => "Alpha antigua",
        _ => Type
    };

    public string DateLabel => ReleaseTime.ToLocalTime().ToString("dd MMM yyyy");
    public string StatusLabel => IsBusy ? "Trabajando…" : IsInstalled ? "Instalada" : "No instalada";
    public string TypeGlyph => Type switch
    {
        "release" => "\uE73E",
        "snapshot" => "\uE896",
        _ => "\uE81C"
    };
}

public sealed class JavaVersionManifest
{
    public string? LatestRelease { get; init; }
    public string? LatestSnapshot { get; init; }
    public List<JavaVersionEntry> Versions { get; init; } = [];
    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class JavaSession
{
    public required bool IsPremium { get; init; }
    public required string PlayerName { get; init; }
    public required string Uuid { get; init; }
    public required string AccessToken { get; init; }
    public string? Notice { get; init; }
}

public sealed class JavaInstallProgress
{
    public required string Stage { get; init; }
    public string Detail { get; init; } = string.Empty;
    public int Current { get; init; }
    public int Total { get; init; }
    public bool Indeterminate => Total <= 0;
    public double Percent => Total > 0 ? Math.Clamp(Current * 100d / Total, 0, 100) : 0;
}

public sealed class JavaRuntimeInfo
{
    public required string Path { get; init; }
    public required int MajorVersion { get; init; }
    public required bool Is64Bit { get; init; }
    public string VersionText { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

public sealed class JavaLaunchResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public int ProcessId { get; init; }
}
