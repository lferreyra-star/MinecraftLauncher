using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using XdLauncher.Models;

namespace XdLauncher.Services;

/// <summary>
/// Reads official Microsoft Store product metadata (updates/changelog/artwork) and official
/// Minecraft launcher news. All network failures degrade gracefully to cached or local content.
/// </summary>
public sealed class StoreCatalogService
{
    private const string ProductId = AppInfo.MinecraftProductId;
    private static readonly TimeSpan MemoryCacheLifetime = TimeSpan.FromMinutes(15);
    private readonly HttpClient _http;
    private readonly object _gate = new();
    private StoreProduct? _cachedProduct;
    private DateTimeOffset _cacheTime = DateTimeOffset.MinValue;

    public StoreCatalogService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "XDLauncher/1.0 (Windows 10; Win64; x64) Microsoft-Store-Client");
    }

    public async Task<StoreProduct?> GetProductAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!force && _cachedProduct is not null && DateTimeOffset.Now - _cacheTime < MemoryCacheLifetime)
                return _cachedProduct;
        }

        var region = GetRegion();
        var locale = GetLocale();
        var urls = new[]
        {
            $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/products/{ProductId}?market={region}&locale={locale}&deviceFamily=Windows.Desktop",
            $"https://displaycatalog.mp.microsoft.com/v7.0/products/{ProductId}?market={region}&languages={locale}&deviceFamily=Windows.Desktop"
        };

        foreach (var url in urls)
        {
            try
            {
                var json = await _http.GetStringAsync(url, cancellationToken);
                var product = ParseProduct(json);
                if (product is not null)
                {
                    lock (_gate)
                    {
                        _cachedProduct = product;
                        _cacheTime = DateTimeOffset.Now;
                    }

                    SaveCache(json);
                    LogService.Info($"Catálogo de Store actualizado: {product.Title} · {product.LastUpdated:u}");
                    return product;
                }
            }
            catch (Exception ex)
            {
                LogService.Warn($"No se pudo leer el catálogo de Store ({url}): {ex.Message}");
            }
        }

        var cached = LoadCache();
        if (cached is not null)
        {
            lock (_gate)
            {
                _cachedProduct = cached;
                _cacheTime = DateTimeOffset.Now;
            }
            return cached;
        }

        return null;
    }

    public async Task<List<NewsItem>> GetNewsAsync(StoreProduct? product, CancellationToken cancellationToken = default)
    {
        var items = await TryGetMojangNewsAsync(cancellationToken);
        if (items.Count > 0)
            return items.Take(4).ToList();

        return await BuildStoreNewsAsync(product, cancellationToken);
    }

    private async Task<List<NewsItem>> TryGetMojangNewsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync("https://launchercontent.mojang.com/v2/news.json", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return [];

            var results = new List<NewsItem>();
            foreach (var entry in entries.EnumerateArray().Take(4))
            {
                var title = GetString(entry, "title");
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                var dateText = GetString(entry, "date");
                DateTimeOffset? date = DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;

                var imageUrl = NormalizeUrl(GetString(entry, "imageurl") ?? GetString(entry, "imageUrl"));
                var imagePath = imageUrl is null ? null : await DownloadImageAsync(imageUrl, "news", cancellationToken);

                results.Add(new NewsItem
                {
                    Title = title,
                    Summary = CleanText(GetString(entry, "text") ?? GetString(entry, "summary") ?? string.Empty),
                    Category = GetString(entry, "category") ?? GetString(entry, "tag") ?? "Minecraft",
                    Date = date,
                    ImagePath = imagePath,
                    Url = NormalizeUrl(GetString(entry, "readmoreurl") ?? GetString(entry, "readMoreUrl")),
                    IsOfficial = true
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            LogService.Info($"Feed de noticias de Mojang no disponible: {ex.Message}");
            return [];
        }
    }

    private async Task<List<NewsItem>> BuildStoreNewsAsync(StoreProduct? product, CancellationToken cancellationToken)
    {
        var items = new List<NewsItem>();
        var hero = product?.HeroImageUrl ?? product?.ImageUrls.FirstOrDefault();
        var heroPath = hero is null ? null : await DownloadImageAsync(hero, "store-hero", cancellationToken);

        if (product is not null)
        {
            var notes = string.IsNullOrWhiteSpace(product.Notes)
                ? "Consulta las novedades y el historial oficial en Microsoft Store."
                : product.Notes;

            items.Add(new NewsItem
            {
                Title = "Minecraft for Windows · actualización oficial",
                Summary = Truncate(notes, 240),
                Category = "Actualización",
                Date = product.LastUpdated,
                ImagePath = heroPath,
                Url = product.StoreUrl,
                IsOfficial = true
            });

            var screenshotUrl = product.ImageUrls.Skip(1).FirstOrDefault() ?? hero;
            var screenshotPath = screenshotUrl is null ? null : await DownloadImageAsync(screenshotUrl, "store-shot", cancellationToken);
            items.Add(new NewsItem
            {
                Title = "Explora el Marketplace oficial",
                Summary = "Descubre mapas, skins, texturas y addons verificados desde el Marketplace integrado en Minecraft.",
                Category = "Marketplace",
                Date = product.LastUpdated,
                ImagePath = screenshotPath,
                Url = "https://www.minecraft.net/marketplace",
                IsOfficial = true
            });
        }
        else
        {
            items.Add(new NewsItem
            {
                Title = "Sin conexión con Microsoft Store",
                Summary = "No se pudo consultar el catálogo oficial. El launcher sigue funcionando con la instalación local.",
                Category = "Aviso",
                ImagePath = Paths.GetBuiltInBackground("bosque"),
                Url = AppInfo.StoreWebUrl,
                IsOfficial = true
            });
        }

        items.Add(new NewsItem
        {
            Title = "XD Launcher 1.0",
            Summary = "Detección de paquetes de Windows, activación oficial, perfiles, gestor de complementos e importación de .mcpack y .mcaddon.",
            Category = "Launcher",
            ImagePath = Paths.GetBuiltInBackground("aurora"),
            Url = AppInfo.Website,
            IsOfficial = false
        });

        return items;
    }

    public async Task<string?> DownloadImageAsync(string? url, string prefix, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUrl(url);
        if (normalized is null)
            return null;

        try
        {
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
                System.Text.Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant()[..16];
            var path = Path.Combine(Paths.ImagesCacheDirectory, $"{prefix}-{hash}.img");
            if (File.Exists(path) && new FileInfo(path).Length > 512)
                return path;

            Directory.CreateDirectory(Paths.ImagesCacheDirectory);
            using var response = await _http.GetAsync(normalized, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var contentLength = response.Content.Headers.ContentLength ?? 0;
            if (contentLength > 12 * 1024 * 1024)
                return null;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(path);
            await source.CopyToAsync(destination, cancellationToken);
            return new FileInfo(path).Length > 512 ? path : null;
        }
        catch (Exception ex)
        {
            LogService.Warn($"No se pudo descargar la imagen del catálogo: {ex.Message}");
            return null;
        }
    }

    private static StoreProduct? ParseProduct(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement p;

            if (root.TryGetProperty("Payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
                p = payload;
            else if (root.TryGetProperty("Product", out var productElement) && productElement.ValueKind == JsonValueKind.Object)
                p = productElement;
            else
                p = root;

            var localized = p.TryGetProperty("LocalizedProperties", out var lp) && lp.ValueKind == JsonValueKind.Array
                ? lp.EnumerateArray().FirstOrDefault()
                : default;

            var title = GetString(p, "Title")
                        ?? (localized.ValueKind == JsonValueKind.Object ? GetString(localized, "ProductTitle") : null)
                        ?? "Minecraft for Windows";
            var shortDescription = GetString(p, "ShortDescription")
                                   ?? (localized.ValueKind == JsonValueKind.Object ? GetString(localized, "ShortDescription") : null)
                                   ?? string.Empty;
            var description = GetString(p, "Description")
                              ?? (localized.ValueKind == JsonValueKind.Object ? GetString(localized, "Description") : null)
                              ?? string.Empty;

            var notes = new List<string>();
            if (p.TryGetProperty("Notes", out var notesElement) && notesElement.ValueKind == JsonValueKind.Array)
            {
                notes.AddRange(notesElement.EnumerateArray()
                    .Where(n => n.ValueKind == JsonValueKind.String)
                    .Select(n => n.GetString() ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n)));
            }

            var lastUpdatedText = GetString(p, "LastUpdateDateUtc")
                                  ?? GetString(p, "LastModifiedDate");
            DateTimeOffset? lastUpdated = DateTimeOffset.TryParse(lastUpdatedText, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsedDate) ? parsedDate : null;

            var images = new List<string>();
            string? hero = null;
            string? logo = null;

            if (p.TryGetProperty("Images", out var imagesElement) && imagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var image in imagesElement.EnumerateArray())
                {
                    var url = NormalizeUrl(GetString(image, "Url") ?? GetString(image, "Uri"));
                    if (url is null)
                        continue;
                    var type = GetString(image, "ImageType") ?? GetString(image, "ImagePurpose") ?? string.Empty;
                    if (type.Contains("logo", StringComparison.OrdinalIgnoreCase))
                        logo ??= url;
                    else if (type.Contains("hero", StringComparison.OrdinalIgnoreCase) ||
                             type.Contains("super", StringComparison.OrdinalIgnoreCase))
                        hero ??= url;
                    else if (type.Contains("screenshot", StringComparison.OrdinalIgnoreCase))
                        images.Add(url);
                }
            }

            if (localized.ValueKind == JsonValueKind.Object &&
                localized.TryGetProperty("Images", out var localizedImages) &&
                localizedImages.ValueKind == JsonValueKind.Array)
            {
                foreach (var image in localizedImages.EnumerateArray())
                {
                    var url = NormalizeUrl(GetString(image, "Uri") ?? GetString(image, "Url"));
                    if (url is null)
                        continue;
                    var purpose = GetString(image, "ImagePurpose") ?? string.Empty;
                    if (purpose.Contains("logo", StringComparison.OrdinalIgnoreCase))
                        logo ??= url;
                    else if (purpose.Contains("hero", StringComparison.OrdinalIgnoreCase) ||
                             purpose.Contains("super", StringComparison.OrdinalIgnoreCase))
                        hero ??= url;
                    else if (purpose.Contains("screenshot", StringComparison.OrdinalIgnoreCase) && !images.Contains(url))
                        images.Add(url);
                }
            }

            var frameworks = new List<string>();
            if (p.TryGetProperty("DisplaySkuAvailabilities", out var skus) && skus.ValueKind == JsonValueKind.Array)
            {
                foreach (var sku in skus.EnumerateArray())
                {
                    if (!sku.TryGetProperty("Sku", out var skuElement))
                        continue;
                    if (!skuElement.TryGetProperty("Properties", out var props) ||
                        !props.TryGetProperty("Packages", out var packages) ||
                        packages.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var package in packages.EnumerateArray())
                    {
                        if (!package.TryGetProperty("FrameworkDependencies", out var deps) ||
                            deps.ValueKind != JsonValueKind.Array)
                            continue;
                        foreach (var dep in deps.EnumerateArray())
                        {
                            var identity = GetString(dep, "PackageIdentity");
                            if (!string.IsNullOrWhiteSpace(identity) && !frameworks.Contains(identity))
                                frameworks.Add(identity);
                        }
                    }
                }
            }

            var notesText = CleanText(string.Join(Environment.NewLine, notes));
            if (string.IsNullOrWhiteSpace(notesText) &&
                p.TryGetProperty("Skus", out var skus2) && skus2.ValueKind == JsonValueKind.Array)
            {
                var skuNotes = new List<string>();
                foreach (var sku in skus2.EnumerateArray())
                {
                    if (sku.TryGetProperty("Properties", out var props) &&
                        props.TryGetProperty("Notes", out var notesArray) &&
                        notesArray.ValueKind == JsonValueKind.Array)
                    {
                        skuNotes.AddRange(notesArray.EnumerateArray()
                            .Where(n => n.ValueKind == JsonValueKind.String)
                            .Select(n => n.GetString() ?? string.Empty));
                    }
                }
                notesText = CleanText(string.Join(Environment.NewLine, skuNotes));
            }

            if (images.Count == 0 && !string.IsNullOrWhiteSpace(hero))
                images.Add(hero);

            var region = GetRegion().ToLowerInvariant();
            var locale = GetLocale();

            return new StoreProduct
            {
                Title = title,
                ShortDescription = CleanText(shortDescription),
                Description = CleanText(description),
                Notes = notesText,
                LastUpdated = lastUpdated,
                ImageUrls = images.Distinct().Take(8).ToList(),
                HeroImageUrl = hero,
                LogoImageUrl = logo,
                StoreUrl = $"https://apps.microsoft.com/detail/{ProductId}?hl={locale}&gl={region}",
                ProductId = ProductId,
                FrameworkDependencies = frameworks
            };
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo interpretar el catálogo de Microsoft Store.");
            return null;
        }
    }

    private void SaveCache(string json)
    {
        try
        {
            Directory.CreateDirectory(Paths.CacheDirectory);
            var path = Path.Combine(Paths.CacheDirectory, "store-product.json");
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, true);
        }
        catch (Exception ex)
        {
            LogService.Warn($"No se pudo guardar la caché del catálogo: {ex.Message}");
        }
    }

    private StoreProduct? LoadCache()
    {
        try
        {
            var path = Path.Combine(Paths.CacheDirectory, "store-product.json");
            return File.Exists(path) ? ParseProduct(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    public static string GetRegion()
    {
        try
        {
            var region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
            return string.IsNullOrWhiteSpace(region) ? "US" : region.ToUpperInvariant();
        }
        catch
        {
            return "US";
        }
    }

    public static string GetLocale()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        return string.IsNullOrWhiteSpace(culture) ? "en-US" : culture;
    }

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        url = url.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal))
            return "https:" + url;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsoluteUri : null;
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var withoutTags = Regex.Replace(text, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
