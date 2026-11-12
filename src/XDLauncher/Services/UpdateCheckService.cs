using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.Services;

public sealed class UpdateCheckService
{
    private const string DefaultGitHubApiUrl = "https://api.github.com";
    private const string DefaultReleasesUrlPattern = "https://api.github.com/repos/{owner}/{repo}/releases/latest";

    private readonly HttpClient _http;
    private readonly string _githubRepo;
    private readonly string _currentVersion;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(5);

    // Estado de verificación
    private DateTimeOffset _lastCheckTime = DateTimeOffset.MinValue;
    private DateTimeOffset _lastNotificationTime = DateTime.Now;
    private string? _lastCheckedVersion;
    private UpdateInfo? _lastUpdateInfo;
    private bool _isChecking;
    private readonly object _gate = new();


    /// <summary>Extrae el nombre owner de una cadena "owner/repo"</summary>
    private static string GetOwnerFromRepo(string repoFullName)
    {
        var parts = repoFullName.Split('/');
        return parts.Length > 0 ? parts[0] : "";
    }

    /// <summary>Extrae el nombre repo de una cadena "owner/repo"</summary>
    private static string GetRepoNameFromRepo(string repoFullName)
    {
        var parts = repoFullName.Split('/');
        return parts.Length > 1 ? parts[1] : repoFullName;
    }
    // Eventos
    public event Action<UpdateInfo>? UpdateChecked;
    public event Action<UpdateInfo, bool>? UpdateAvailable;
    public event Action<string>? StatusMessage;

    // Configuración
    public TimeSpan CheckInterval => _checkInterval;
    public bool IsChecking
    {
        get => _isChecking;
        private set
        {
            if (_isChecking != value)
            {
                _isChecking = value;
                StatusMessage?.Invoke(_isChecking ? "Comprobando actualizaciones..." : "");
            }
        }
    }

    public UpdateCheckService(string githubRepo, string currentVersion)
    {
        _githubRepo = githubRepo;
        _currentVersion = currentVersion;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>Comprobar actualizaciones ahora.</summary>
    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // Evitar verificaciones demasiado frecuentes
        lock (_gate)
        {
            var timeSinceLastCheck = DateTimeOffset.Now - _lastCheckTime;
            if (timeSinceLastCheck < _checkInterval && _lastCheckedVersion is not null)
            {
                // Ya comprobamos recientemente, usar información cached
                StatusMessage?.Invoke($"Última comprobación: hace {((DateTimeOffset.Now - _lastCheckTime).TotalMinutes):F0} min.");
                UpdateChecked?.Invoke(_lastUpdateInfo ?? new UpdateInfo { Availability = UpdateAvailability.Unknown });
                return;
            }
        }

        try
        {
            IsChecking = true;
            _lastCheckTime = DateTimeOffset.Now;

            StatusMessage?.Invoke("Consultando GitHub Releases...");

            // Construir URL
            var releasesUrl = DefaultReleasesUrlPattern.Replace("{owner}", GetOwnerFromRepo(_githubRepo))
                .Replace("{repo}", GetRepoNameFromRepo(_githubRepo));

            using var request = new HttpRequestMessage(HttpMethod.Get, releasesUrl);
            request.Headers.Add("User-Agent", "XDLauncher-Updater");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                StatusMessage?.Invoke("Error al consultar GitHub API.");
                _lastCheckedVersion = _currentVersion;
                _lastUpdateInfo = new UpdateInfo { Availability = UpdateAvailability.Unknown };
                UpdateChecked?.Invoke(_lastUpdateInfo);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Obtener información de la versión
            string latestTagName = root.GetProperty("tag_name").GetString() ?? "";
            // Quitar prefijo 'v' si existe
            if (latestTagName.StartsWith("v"))
                latestTagName = latestTagName[1..];

            string latestName = root.GetProperty("name").GetString() ?? "";
            string body = root.GetProperty("body").GetString() ?? "";
            string publishedAt = root.GetProperty("published_at").GetString() ?? DateTimeOffset.Now.ToString("o");

            // Obtener el asset de descarga para Windows
            string? downloadUrl = "";
            string sha256 = "";

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName.EndsWith(".exe") || assetName.EndsWith(".zip"))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        // Intentar obtener SHA-256 si el asset lo declara
                        if (asset.TryGetProperty("sha256", out var sha256Prop) && sha256Prop.ValueKind == JsonValueKind.String)
                        {
                            sha256 = sha256Prop.GetString() ?? "";
                        }
                        break;
                    }
                }
            }

            // Comparar versiones usando System.Version
            var latestVersion = Version.TryParse(latestTagName, out var latestParsed) ? latestParsed : new Version(0, 0, 0);
            var installedVersion = Version.TryParse(_currentVersion, out var installedParsed) ? installedParsed : new Version(0, 0, 0);

            UpdateAvailability availability;
            bool isAvailable = false;

            if (latestVersion > installedVersion)
            {
                availability = UpdateAvailability.Available;
                isAvailable = true;
            }
            else if (latestVersion == installedVersion)
            {
                availability = UpdateAvailability.UpToDate;
                isAvailable = false;
            }
            else
            {
                // La versión local es mayor que la publicada (versión de desarrollo o beta)
                availability = UpdateAvailability.UpToDate;
                isAvailable = false;
            }

            var info = new UpdateInfo
            {
                Availability = availability,
                LatestVersion = latestTagName,
                InstalledVersion = _currentVersion,
                ReleaseNotes = body,
                ReleaseDate = DateTimeOffset.TryParse(publishedAt, out var releaseDate) ? releaseDate : DateTimeOffset.Now,
                DownloadUrl = downloadUrl,
                Sha256 = sha256,
                LatestName = latestName
            };

            // Almacenar para uso futuro
            lock (_gate)
            {
                _lastCheckedVersion = latestTagName;
                _lastUpdateInfo = info;
            }

            StatusMessage?.Invoke(info.Availability == UpdateAvailability.Available
                ? $"Versión {latestTagName} disponible"
                : $"La versión {_currentVersion} está actualizada");

            UpdateChecked?.Invoke(info);
            UpdateAvailable?.Invoke(info, isAvailable);
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke($"Error verificando actualizaciones: {ex.Message}");
            _lastCheckedVersion = _currentVersion;
            _lastUpdateInfo = new UpdateInfo { Availability = UpdateAvailability.Unknown };
            UpdateChecked?.Invoke(_lastUpdateInfo);
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>Obtener información de la última versión verificada.</summary>
    public UpdateInfo? GetLastUpdateInfo() => _lastUpdateInfo;

    /// <summary>Marcar que el usuario ha visto la actualización (para evitar duplicados).</summary>
    public void MarkNotificationSeen()
    {
        _lastNotificationTime = DateTime.Now;}}