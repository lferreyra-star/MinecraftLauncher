using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Web;

namespace XdLauncher.Services;

public sealed record ModrinthProject(
    string Id,
    string Slug,
    string Name,
    string? Description,
    string? Author,
    string? IconUrl,
    long Downloads,
    int Followers,
    string? UpdatedAt,
    string? SourceUrl,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Versions);

public sealed record ModrinthFile(
    string FileName,
    string Url,
    long Size,
    string? Sha512,
    string? Sha1,
    bool Primary);

public sealed record ModrinthDependency(
    string? ProjectId,
    string? VersionId,
    string Type);

public sealed record ModrinthVersion(
    string Id,
    string ProjectId,
    string Name,
    string VersionNumber,
    string? DatePublished,
    IReadOnlyList<string> Loaders,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<ModrinthFile> Files,
    IReadOnlyList<ModrinthDependency> Dependencies);

public sealed record ModrinthSearchPage(
    IReadOnlyList<ModrinthProject> Projects,
    int Total,
    int Offset,
    int Limit);

/// <summary>Cliente real de la API pública de Modrinth v2. No usa datos inventados.</summary>
public sealed class ModrinthService
{
    private const string BaseUrl = "https://api.modrinth.com/v2";
    private const string Website = "https://modrinth.com/mod/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly Dictionary<string, (DateTimeOffset At, string Json)> _cache = new();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(10);

    public ModrinthService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("XDLauncher/1.3 (https://xdluncher.gt.tc)");
        _http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<string>> GetGameVersionsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetJsonAsync("/tag/game_version", cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var result = new List<string>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var version = GetString(item, "version");
            var type = GetString(item, "version_type");
            if (!string.IsNullOrWhiteSpace(version) && string.Equals(type, "release", StringComparison.OrdinalIgnoreCase))
                result.Add(version);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetLoadersAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetJsonAsync("/tag/loader", cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            JavaModLoaders.Fabric, JavaModLoaders.Forge, JavaModLoaders.NeoForge, JavaModLoaders.Quilt
        };
        var result = new List<string>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var name = GetString(item, "name");
            if (!string.IsNullOrWhiteSpace(name) && supported.Contains(name))
                result.Add(name);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<ModrinthSearchPage> SearchAsync(
        string query,
        string? gameVersion,
        string? loader,
        string? category,
        string sort,
        int offset,
        int limit,
        CancellationToken cancellationToken = default,
        string projectType = "mod")
    {
        var facets = new List<List<string>> { new() { $"project_type:{projectType}" } };

        if (!string.IsNullOrWhiteSpace(gameVersion))
            facets.Add(new List<string> { $"versions:{NormalizeGameVersion(gameVersion)}" });
        if (!string.IsNullOrWhiteSpace(loader))
            facets.Add(new List<string> { $"loaders:{loader.ToLowerInvariant()}" });
        if (!string.IsNullOrWhiteSpace(category))
            facets.Add(new List<string> { $"categories:{category.ToLowerInvariant()}" });

        var facetsJson = JsonSerializer.Serialize(facets);
        var index = sort switch
        {
            "Recientes" => "updated",
            "Popularidad" => "downloads",
            _ => "relevance"
        };

        var queryString = new Dictionary<string, string>
        {
            ["query"] = query ?? string.Empty,
            ["facets"] = facetsJson,
            ["index"] = index,
            ["offset"] = offset.ToString(),
            ["limit"] = Math.Clamp(limit, 1, 100).ToString()
        }.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}");

        var json = await GetJsonAsync($"/search?{string.Join("&", queryString)}", cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var hits = root.TryGetProperty("hits", out var hitsElement) && hitsElement.ValueKind == JsonValueKind.Array
            ? hitsElement.EnumerateArray().Select(ParseProject).Where(p => p is not null).Cast<ModrinthProject>().ToList()
            : new List<ModrinthProject>();

        return new ModrinthSearchPage(
            hits,
            root.TryGetProperty("total_hits", out var total) && total.TryGetInt32(out var n) ? n : hits.Count,
            offset,
            limit);
    }

    public async Task<ModrinthProject?> GetProjectAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
        var json = await GetJsonAsync($"/project/{Uri.EscapeDataString(idOrSlug)}", cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return ParseProject(doc.RootElement);
    }

    public async Task<IReadOnlyList<ModrinthVersion>> GetVersionsAsync(
        string projectId,
        string? gameVersion,
        string? loader,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(gameVersion))
            parameters.Add($"game_versions={Uri.EscapeDataString($"[\"{NormalizeGameVersion(gameVersion)}\"]")}");
        if (!string.IsNullOrWhiteSpace(loader))
            parameters.Add($"loaders={Uri.EscapeDataString($"[\"{loader.ToLowerInvariant()}\"]")}");

        var suffix = parameters.Count > 0 ? "?" + string.Join("&", parameters) : string.Empty;
        var json = await GetJsonAsync($"/project/{Uri.EscapeDataString(projectId)}/version{suffix}", cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(ParseVersion).Where(v => v is not null).Cast<ModrinthVersion>().ToList();
    }

    public async Task<ModrinthVersion?> GetVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var json = await GetJsonAsync($"/version/{Uri.EscapeDataString(versionId)}", cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return ParseVersion(doc.RootElement);
    }

    public static bool IsCompatible(ModrinthVersion version, string gameVersion, string loader)
    {
        var versionOk = version.GameVersions.Any(v =>
            string.Equals(v, gameVersion, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, NormalizeGameVersion(gameVersion), StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith(NormalizeGameVersion(gameVersion) + ".", StringComparison.OrdinalIgnoreCase));
        var loaderOk = version.Loaders.Any(l => string.Equals(l, loader, StringComparison.OrdinalIgnoreCase));
        return versionOk && loaderOk;
    }

    public static string NormalizeGameVersion(string version)
        => string.IsNullOrWhiteSpace(version) ? version : version.Trim();

    private async Task<string> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        var key = BaseUrl + path;
        if (_cache.TryGetValue(key, out var cached) && DateTimeOffset.UtcNow - cached.At < _cacheTtl)
            return cached.Json;

        using var response = await _http.GetAsync(key, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        _cache[key] = (DateTimeOffset.UtcNow, json);
        return json;
    }

    private static ModrinthProject? ParseProject(JsonElement element)
    {
        var id = GetString(element, "id") ?? GetString(element, "project_id");
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var categories = element.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array
            ? cats.EnumerateArray().Select(c => c.GetString() ?? string.Empty).Where(s => s.Length > 0).ToArray()
            : Array.Empty<string>();

        return new ModrinthProject(
            id,
            GetString(element, "slug") ?? id,
            GetString(element, "title") ?? id,
            GetString(element, "description"),
            GetString(element, "author"),
            GetString(element, "icon_url"),
            GetInt64(element, "downloads"),
            GetInt32(element, "followers"),
            GetString(element, "updated") ?? GetString(element, "date_modified"),
            Website + (GetString(element, "slug") ?? id),
            categories,
            ReadStringArray(element, "versions"));
    }

    private static ModrinthVersion? ParseVersion(JsonElement element)
    {
        var id = GetString(element, "id");
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var files = element.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array
            ? filesElement.EnumerateArray().Select(file => new ModrinthFile(
                GetString(file, "filename") ?? "mod.jar",
                GetString(file, "url") ?? string.Empty,
                GetInt64(file, "size"),
                ReadHash(file, "sha512"),
                ReadHash(file, "sha1"),
                file.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True)).ToArray()
            : Array.Empty<ModrinthFile>();

        var dependencies = element.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array
            ? deps.EnumerateArray().Select(dep => new ModrinthDependency(
                GetString(dep, "project_id"),
                GetString(dep, "version_id"),
                GetString(dep, "dependency_type") ?? "required")).ToArray()
            : Array.Empty<ModrinthDependency>();

        return new ModrinthVersion(
            id,
            GetString(element, "project_id") ?? string.Empty,
            GetString(element, "name") ?? id,
            GetString(element, "version_number") ?? id,
            GetString(element, "date_published"),
            ReadStringArray(element, "loaders"),
            ReadStringArray(element, "game_versions"),
            files,
            dependencies);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(v => v.GetString() ?? string.Empty).Where(s => s.Length > 0).ToArray()
            : Array.Empty<string>();

    private static string? ReadHash(JsonElement file, string name)
        => file.TryGetProperty("hashes", out var hashes) ? GetString(hashes, name) : null;

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long GetInt64(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : 0;

    private static int GetInt32(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : 0;
}
