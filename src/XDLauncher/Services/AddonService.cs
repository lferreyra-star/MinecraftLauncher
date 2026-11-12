using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using XdLauncher.Models;

namespace XdLauncher.Services;

/// <summary>
/// Scans Minecraft's official com.mojang user folders and imports .mcpack/.mcaddon/
/// .mcworld/.mctemplate archives into the folders Minecraft itself reads.
/// </summary>
public sealed class AddonService
{
    private static readonly Dictionary<string, AddonCategory> FolderCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["resource_packs"] = AddonCategory.ResourcePack,
        ["behavior_packs"] = AddonCategory.BehaviorPack,
        ["skin_packs"] = AddonCategory.SkinPack,
        ["world_templates"] = AddonCategory.WorldTemplate,
        ["minecraftWorlds"] = AddonCategory.Map
    };

    private static readonly string[] SupportedExtensions = [".mcpack", ".mcaddon", ".mcworld", ".mctemplate"];

    public IReadOnlyList<string> SupportedFileExtensions => SupportedExtensions;

    public List<AddonItem> ScanInstalled()
    {
        var items = new List<AddonItem>();
        foreach (var path in Paths.ExistingComMojangPaths())
        {
            var channel = path.Contains("MinecraftWindowsBeta", StringComparison.OrdinalIgnoreCase) ? "Preview" : "Retail";
            foreach (var (folder, category) in FolderCategories)
            {
                var root = System.IO.Path.Combine(path, folder);
                if (!Directory.Exists(root))
                    continue;

                try
                {
                    foreach (var directory in Directory.EnumerateDirectories(root))
                    {
                        try
                        {
                            if (folder == "minecraftWorlds")
                            {
                                var world = ReadWorldFolder(directory, channel);
                                if (world is not null)
                                    items.Add(world);
                                continue;
                            }

                            var item = ReadInstalledPack(directory, category, channel);
                            if (item is not null)
                                items.Add(item);
                        }
                        catch (Exception ex)
                        {
                            LogService.Warn($"No se pudo leer el complemento instalado '{directory}': {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warn($"No se pudo explorar '{root}': {ex.Message}");
                }
            }
        }

        return items
            .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public List<AddonItem> ScanLibrary()
    {
        var items = new List<AddonItem>();
        try
        {
            if (!Directory.Exists(Paths.LibraryDirectory))
                return items;

            foreach (var file in Directory.EnumerateFiles(Paths.LibraryDirectory)
                         .Where(f => SupportedExtensions.Contains(System.IO.Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
            {
                try
                {
                    items.AddRange(ReadArchiveMetadata(file, isInstalled: false));
                }
                catch (Exception ex)
                {
                    LogService.Warn($"No se pudo leer el archivo de biblioteca '{file}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo explorar la biblioteca de complementos.");
        }

        return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public List<AddonItem> ReadArchiveMetadata(string filePath, bool isInstalled)
    {
        var items = new List<AddonItem>();
        if (!File.Exists(filePath))
            return items;

        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        using var archive = ZipFile.OpenRead(filePath);

        if (extension is ".mcworld" or ".mctemplate")
        {
            var world = ReadArchiveWorld(archive, filePath, extension);
            if (world is not null)
                items.Add(world);
            return items;
        }

        foreach (var entry in FindManifestEntries(archive))
        {
            try
            {
                var meta = ReadManifestFromArchive(archive, entry.FullName);
                if (meta is null)
                    continue;

                var iconPath = ExtractIcon(archive, entry.FullName, filePath, "pack_icon");
                items.Add(new AddonItem
                {
                    Key = $"{filePath}::{entry.FullName}",
                    Name = meta.Name,
                    Description = meta.Description,
                    Category = meta.Category,
                    Version = meta.Version,
                    Uuid = meta.Uuid,
                    SourceFile = filePath,
                    ManifestPath = entry.FullName,
                    IsInstalled = isInstalled,
                    IconPath = iconPath,
                    ModuleTypes = meta.ModuleTypes,
                    ExtraInfo = meta.FormatVersion
                });
            }
            catch (Exception ex)
            {
                LogService.Warn($"Manifiesto inválido en '{filePath}' ({entry.FullName}): {ex.Message}");
            }
        }

        return items;
    }

    public async Task<List<AddonImportResult>> ImportFileAsync(string filePath, InstallationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AddonImportResult>();
        if (!File.Exists(filePath))
        {
            results.Add(new AddonImportResult
            {
                Name = System.IO.Path.GetFileName(filePath),
                Success = false,
                Message = "El archivo no existe."
            });
            return results;
        }

        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(new AddonImportResult
            {
                Name = System.IO.Path.GetFileName(filePath),
                Success = false,
                Message = "Formato no compatible. Usa .mcpack, .mcaddon, .mcworld o .mctemplate."
            });
            return results;
        }

        try
        {
            var root = GetTargetComMojang(channel);
            Directory.CreateDirectory(root);

            using var archive = ZipFile.OpenRead(filePath);
            ValidateArchiveSize(archive);

            if (extension == ".mcworld")
            {
                results.Add(ImportWorld(archive, root, filePath, "minecraftWorlds"));
                return results;
            }

            if (extension == ".mctemplate")
            {
                results.Add(ImportWorld(archive, root, filePath, "world_templates"));
                return results;
            }

            var manifestEntries = FindManifestEntries(archive).ToList();
            if (manifestEntries.Count == 0)
            {
                results.Add(new AddonImportResult
                {
                    Name = System.IO.Path.GetFileName(filePath),
                    Success = false,
                    Message = "El archivo no contiene manifest.json. No es un paquete válido de Minecraft Bedrock."
                });
                return results;
            }

            // Pre-read metadata and build a UUID remap table for duplicates.
            var prepared = new List<(ZipArchiveEntry Entry, PackMeta Meta, string PackRoot)>();
            foreach (var entry in manifestEntries)
            {
                var meta = ReadManifestFromArchive(archive, entry.FullName);
                if (meta is not null)
                    prepared.Add((entry, meta, GetPackRoot(entry.FullName)));
            }

            var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, meta, _) in prepared)
            {
                if (!string.IsNullOrWhiteSpace(meta.Uuid) && PackUuidExists(root, meta.Category, meta.Uuid))
                    remap[meta.Uuid] = Guid.NewGuid().ToString();
            }

            foreach (var (entry, meta, packRoot) in prepared)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await Task.Run(() => ExtractAndInstallPack(archive, entry, meta, packRoot, root, remap), cancellationToken);
                results.Add(result);
            }
        }
        catch (InvalidDataException)
        {
            results.Add(new AddonImportResult
            {
                Name = System.IO.Path.GetFileName(filePath),
                Success = false,
                Message = "El archivo ZIP está dañado o protegido con contraseña."
            });
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"Error importando '{filePath}'.");
            results.Add(new AddonImportResult
            {
                Name = System.IO.Path.GetFileName(filePath),
                Success = false,
                Message = "Error al importar: " + ex.Message
            });
        }

        return results;
    }

    private AddonImportResult ExtractAndInstallPack(ZipArchive archive, ZipArchiveEntry manifestEntry,
        PackMeta meta, string packRoot, string comMojangRoot, Dictionary<string, string> remap)
    {
        var baseFolder = meta.Category switch
        {
            AddonCategory.ResourcePack => "resource_packs",
            AddonCategory.BehaviorPack => "behavior_packs",
            AddonCategory.SkinPack => "skin_packs",
            AddonCategory.WorldTemplate => "world_templates",
            _ => "resource_packs"
        };

        var destinationRoot = System.IO.Path.Combine(comMojangRoot, baseFolder);
        Directory.CreateDirectory(destinationRoot);

        var safeName = SanitizeFolderName(meta.Name);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "Paquete";
        if (safeName.Length > 48)
            safeName = safeName[..48].Trim();

        var suffix = string.IsNullOrWhiteSpace(meta.Uuid) ? Guid.NewGuid().ToString("N")[..8] : meta.Uuid[..8];
        var folderName = $"{safeName}-{suffix}";
        var destination = System.IO.Path.Combine(destinationRoot, folderName);
        var unique = 1;
        while (Directory.Exists(destination))
            destination = System.IO.Path.Combine(destinationRoot, $"{folderName}-{unique++}");

        try
        {
            ExtractSubtree(archive, packRoot, destination, skipManifest: true);

            var manifestPath = System.IO.Path.Combine(destination, "manifest.json");
            var manifestText = ReadEntryText(manifestEntry);
            var manifestNode = JsonNode.Parse(manifestText) as JsonObject
                               ?? throw new InvalidDataException("manifest.json inválido.");

            if (remap.Count > 0)
                RewriteUuids(manifestNode, remap);

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(manifestPath, manifestNode.ToJsonString(options), new UTF8Encoding(false));

            var iconSource = FindIconEntry(archive, manifestEntry.FullName, "pack_icon");
            if (iconSource is not null)
            {
                var iconDestination = System.IO.Path.Combine(destination, System.IO.Path.GetFileName(iconSource.FullName));
                iconSource.ExtractToFile(iconDestination, overwrite: true);
            }

            LogService.Info($"Complemento importado: {meta.Name} → {destination}");
            return new AddonImportResult
            {
                Name = meta.Name,
                Success = true,
                Category = meta.Category,
                Destination = destination,
                Message = $"Instalado en {baseFolder}."
            };
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"No se pudo instalar el complemento '{meta.Name}'.");
            try
            {
                if (Directory.Exists(destination))
                    Directory.Delete(destination, recursive: true);
            }
            catch
            {
                // ignore cleanup failure
            }

            return new AddonImportResult
            {
                Name = meta.Name,
                Success = false,
                Category = meta.Category,
                Message = ex.Message
            };
        }
    }

    private AddonImportResult ImportWorld(ZipArchive archive, string comMojangRoot, string filePath, string folder)
    {
        try
        {
            var levelName = ReadEntryTextByName(archive, "levelname.txt");
            var baseName = string.IsNullOrWhiteSpace(levelName)
                ? System.IO.Path.GetFileNameWithoutExtension(filePath)
                : levelName.Trim();
            var safeName = SanitizeFolderName(baseName);
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "Mundo";
            if (safeName.Length > 48)
                safeName = safeName[..48].Trim();

            var root = System.IO.Path.Combine(comMojangRoot, folder);
            Directory.CreateDirectory(root);
            var destination = System.IO.Path.Combine(root, $"{safeName}-{Guid.NewGuid().ToString("N")[..8]}");
            ExtractSubtree(archive, string.Empty, destination, skipManifest: false);

            if (folder == "minecraftWorlds" && !File.Exists(System.IO.Path.Combine(destination, "level.dat")))
            {
                Directory.Delete(destination, recursive: true);
                return new AddonImportResult
                {
                    Name = baseName,
                    Success = false,
                    Category = AddonCategory.Map,
                    Message = "El archivo no contiene level.dat. No parece un mundo válido."
                };
            }

            LogService.Info($"Mundo importado: {baseName} → {destination}");
            return new AddonImportResult
            {
                Name = baseName,
                Success = true,
                Category = AddonCategory.Map,
                Destination = destination,
                Message = folder == "minecraftWorlds" ? "Mundo importado." : "Plantilla de mundo importada."
            };
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"No se pudo importar el mundo '{filePath}'.");
            return new AddonImportResult
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(filePath),
                Success = false,
                Category = AddonCategory.Map,
                Message = ex.Message
            };
        }
    }

    private static void ExtractSubtree(ZipArchive archive, string rootPrefix, string destination, bool skipManifest)
    {
        var normalizedPrefix = rootPrefix.Replace('\\', '/').TrimEnd('/');
        if (normalizedPrefix.Length > 0)
            normalizedPrefix += "/";

        var destinationFull = System.IO.Path.GetFullPath(destination);
        Directory.CreateDirectory(destinationFull);

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase) || name.EndsWith("/", StringComparison.Ordinal))
                continue;

            var relative = name[normalizedPrefix.Length..];
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains(':'))
                continue;
            if (skipManifest && relative.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(destinationFull, relative));
            if (!target.StartsWith(destinationFull + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !target.Equals(destinationFull, StringComparison.OrdinalIgnoreCase))
            {
                LogService.Warn($"Entrada ZIP rechazada por ruta insegura: {entry.FullName}");
                continue;
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private AddonItem? ReadInstalledPack(string directory, AddonCategory fallbackCategory, string channel)
    {
        var manifestPath = System.IO.Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
            return null;

        var text = File.ReadAllText(manifestPath);
        var meta = ParseManifest(text, directory);
        if (meta is null)
            return null;

        var icon = FindInstalledIcon(directory, "pack_icon");
        return new AddonItem
        {
            Key = directory,
            Name = meta.Name,
            Description = meta.Description,
            Category = meta.Category == AddonCategory.Addon && fallbackCategory != AddonCategory.Addon
                ? fallbackCategory
                : meta.Category,
            Version = meta.Version,
            Uuid = meta.Uuid,
            FolderPath = directory,
            IsInstalled = true,
            IconPath = icon,
            GameChannel = channel,
            ModuleTypes = meta.ModuleTypes,
            ExtraInfo = meta.FormatVersion
        };
    }

    private AddonItem? ReadWorldFolder(string directory, string channel)
    {
        if (!File.Exists(System.IO.Path.Combine(directory, "level.dat")))
            return null;

        var name = ReadFileText(System.IO.Path.Combine(directory, "levelname.txt"))
                   ?? System.IO.Path.GetFileName(directory);
        var icon = FindInstalledIcon(directory, "world_icon");
        return new AddonItem
        {
            Key = directory,
            Name = name.Trim(),
            Description = "Mundo local de Minecraft Bedrock.",
            Category = AddonCategory.Map,
            Version = "Mundo",
            Uuid = System.IO.Path.GetFileName(directory),
            FolderPath = directory,
            IsInstalled = true,
            IsWorld = true,
            IconPath = icon,
            GameChannel = channel,
            ExtraInfo = "Mundo local"
        };
    }

    private static AddonItem? ReadArchiveWorld(ZipArchive archive, string filePath, string extension)
    {
        var name = ReadEntryTextByName(archive, "levelname.txt");
        if (string.IsNullOrWhiteSpace(name))
            name = System.IO.Path.GetFileNameWithoutExtension(filePath);

        var icon = ExtractIcon(archive, "levelname.txt", filePath, "world_icon");
        var hasLevelDat = archive.Entries.Any(e =>
            e.FullName.Equals("level.dat", StringComparison.OrdinalIgnoreCase));

        if (extension == ".mcworld" && !hasLevelDat)
            return null;

        return new AddonItem
        {
            Key = filePath,
            Name = name.Trim(),
            Description = extension == ".mcworld" ? "Mundo para importar en Minecraft." : "Plantilla de mundo para importar.",
            Category = extension == ".mcworld" ? AddonCategory.Map : AddonCategory.WorldTemplate,
            Version = extension == ".mcworld" ? "Mundo" : "Plantilla",
            Uuid = filePath,
            SourceFile = filePath,
            IsInstalled = false,
            IsWorld = true,
            IconPath = icon,
            ExtraInfo = extension.TrimStart('.').ToUpperInvariant()
        };
    }

    private static PackMeta? ReadManifestFromArchive(ZipArchive archive, string manifestEntryName)
    {
        var entry = archive.GetEntry(manifestEntryName)
                    ?? archive.Entries.FirstOrDefault(e => e.FullName.Equals(manifestEntryName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return null;

        var text = ReadEntryText(entry);
        var lang = ReadArchiveLanguage(archive);
        return ParseManifest(text, string.Empty, lang);
    }

    private static PackMeta? ParseManifest(string text, string? directory, Dictionary<string, string>? lang = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (!root.TryGetProperty("header", out var header) || header.ValueKind != JsonValueKind.Object)
                return null;

            lang ??= directory is null ? new Dictionary<string, string>() : ReadFolderLanguage(directory);

            var nameRaw = GetString(header, "name") ?? "Paquete sin nombre";
            var descriptionRaw = GetString(header, "description") ?? string.Empty;
            var uuid = GetString(header, "uuid") ?? string.Empty;
            var version = ReadVersion(header);
            var formatVersion = root.TryGetProperty("format_version", out var fv) ? fv.ToString() : string.Empty;

            var modules = new List<string>();
            var categories = new List<AddonCategory>();
            if (root.TryGetProperty("modules", out var moduleArray) && moduleArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var module in moduleArray.EnumerateArray())
                {
                    var type = GetString(module, "type") ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(type))
                        modules.Add(type);
                    categories.Add(type.ToLowerInvariant() switch
                    {
                        "resources" => AddonCategory.ResourcePack,
                        "data" or "script" => AddonCategory.BehaviorPack,
                        "skin" => AddonCategory.SkinPack,
                        "world_template" => AddonCategory.WorldTemplate,
                        _ => AddonCategory.Addon
                    });
                }
            }

            var distinct = categories.Distinct().ToList();
            var category = distinct.Count switch
            {
                0 => AddonCategory.Addon,
                1 => distinct[0],
                _ when distinct.Contains(AddonCategory.ResourcePack) && distinct.Contains(AddonCategory.BehaviorPack)
                    => AddonCategory.Addon,
                _ => distinct[0]
            };

            return new PackMeta
            {
                Name = ResolveLocalized(nameRaw, lang),
                Description = ResolveLocalized(descriptionRaw, lang),
                Uuid = uuid,
                Version = version,
                Category = category,
                ModuleTypes = modules,
                FormatVersion = string.IsNullOrWhiteSpace(formatVersion) ? null : $"format {formatVersion}"
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveLocalized(string value, Dictionary<string, string> lang)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (lang.TryGetValue(value, out var translation) && !string.IsNullOrWhiteSpace(translation))
            return StripFormatting(translation);

        var cleaned = StripFormatting(value);
        if (cleaned.StartsWith("pack.", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("pack_", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[5..].Replace('.', ' ').Replace('_', ' ').Trim();
            if (cleaned.Length > 0)
                cleaned = char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
        }

        return cleaned;
    }

    private static string StripFormatting(string value)
        => Regex.Replace(value, "§.", string.Empty).Trim();

    private static string ReadVersion(JsonElement header)
    {
        if (!header.TryGetProperty("version", out var version))
            return "1.0.0";

        if (version.ValueKind == JsonValueKind.Array)
            return string.Join('.', version.EnumerateArray().Select(v => v.ToString()));
        return version.ToString();
    }

    private static Dictionary<string, string> ReadFolderLanguage(string directory)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var texts = System.IO.Path.Combine(directory, "texts");
            if (!Directory.Exists(texts))
                return result;

            var files = new[] { "es_ES.lang", "es_MX.lang", "en_US.lang", "en_GB.lang" }
                .Select(f => System.IO.Path.Combine(texts, f))
                .Where(File.Exists)
                .Concat(Directory.EnumerateFiles(texts, "*.lang").OrderBy(f => f))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                foreach (var line in File.ReadLines(file))
                {
                    var split = line.IndexOf('=');
                    if (split <= 0)
                        continue;
                    var key = line[..split].Trim();
                    var value = line[(split + 1)..].Trim();
                    if (key.Length > 0 && !result.ContainsKey(key))
                        result[key] = value;
                }
            }
        }
        catch
        {
            // localization is optional
        }

        return result;
    }

    private static Dictionary<string, string> ReadArchiveLanguage(ZipArchive archive)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[] { "texts/es_ES.lang", "texts/es_MX.lang", "texts/en_US.lang", "texts/en_GB.lang" })
        {
            var entry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                continue;

            foreach (var line in ReadEntryText(entry).Split('\n'))
            {
                var split = line.IndexOf('=');
                if (split <= 0)
                    continue;
                var key = line[..split].Trim();
                var value = line[(split + 1)..].Trim();
                if (key.Length > 0 && !result.ContainsKey(key))
                    result[key] = value;
            }
        }
        return result;
    }

    private static IEnumerable<ZipArchiveEntry> FindManifestEntries(ZipArchive archive)
        => archive.Entries.Where(e =>
            e.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
            e.FullName.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase));

    private static string GetPackRoot(string manifestEntryName)
    {
        var normalized = manifestEntryName.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalized[..lastSlash];
    }

    private static ZipArchiveEntry? FindIconEntry(ZipArchive archive, string referenceEntry, string prefix)
    {
        var root = GetPackRoot(referenceEntry);
        var expected = string.IsNullOrEmpty(root) ? $"{prefix}.png" : $"{root}/{prefix}.png";
        var entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals(expected, StringComparison.OrdinalIgnoreCase));
        if (entry is not null)
            return entry;

        return archive.Entries.FirstOrDefault(e =>
            System.IO.Path.GetFileName(e.FullName).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            e.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractIcon(ZipArchive archive, string referenceEntry, string sourceKey, string prefix)
    {
        try
        {
            var icon = FindIconEntry(archive, referenceEntry, prefix);
            if (icon is null)
                return null;

            var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
                Encoding.UTF8.GetBytes($"{sourceKey}::{icon.FullName}::{icon.Length}"))).ToLowerInvariant()[..16];
            var destination = System.IO.Path.Combine(Paths.AddonCacheDirectory, $"icon-{hash}.png");
            if (!File.Exists(destination))
            {
                Directory.CreateDirectory(Paths.AddonCacheDirectory);
                icon.ExtractToFile(destination, overwrite: true);
            }
            return File.Exists(destination) ? destination : null;
        }
        catch (Exception ex)
        {
            LogService.Warn($"No se pudo extraer el icono del complemento: {ex.Message}");
            return null;
        }
    }

    private static string? FindInstalledIcon(string directory, string prefix)
    {
        try
        {
            var files = Directory.EnumerateFiles(directory, prefix + ".*")
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return files.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string? ReadEntryTextByName(ZipArchive archive, string name)
    {
        var entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : ReadEntryText(entry);
    }

    private static string? ReadFileText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
            builder.Append(invalid.Contains(c) || c is '\r' or '\n' or '\t' ? '-' : c);
        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim(' ', '.', '-');
    }

    private static bool PackUuidExists(string comMojangRoot, AddonCategory category, string uuid)
    {
        var folder = category switch
        {
            AddonCategory.ResourcePack or AddonCategory.Addon => "resource_packs",
            AddonCategory.BehaviorPack => "behavior_packs",
            AddonCategory.SkinPack => "skin_packs",
            AddonCategory.WorldTemplate => "world_templates",
            _ => "resource_packs"
        };

        var root = System.IO.Path.Combine(comMojangRoot, folder);
        if (!Directory.Exists(root))
            return false;

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var manifest = System.IO.Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifest))
                continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (doc.RootElement.TryGetProperty("header", out var header) &&
                    GetString(header, "uuid")?.Equals(uuid, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }
            catch
            {
                // ignore broken manifests
            }
        }

        return false;
    }

    private static void RewriteUuids(JsonNode node, Dictionary<string, string> remap)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (property.Value is JsonValue value &&
                        value.TryGetValue<string>(out var text) &&
                        remap.TryGetValue(text, out var replacement))
                    {
                        obj[property.Key] = replacement;
                    }
                    else if (property.Value is not null)
                    {
                        RewriteUuids(property.Value, remap);
                    }
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is JsonValue value &&
                        value.TryGetValue<string>(out var text) &&
                        remap.TryGetValue(text, out var replacement))
                    {
                        array[i] = replacement;
                    }
                    else if (array[i] is not null)
                    {
                        RewriteUuids(array[i]!, remap);
                    }
                }
                break;
        }
    }

    private static string GetTargetComMojang(InstallationChannel channel)
    {
        var family = channel == InstallationChannel.Preview ? Paths.PreviewPackageFamily : Paths.RetailPackageFamily;
        var expected = Paths.GetComMojangPathOrExpected(family);
        Directory.CreateDirectory(expected);
        return expected;
    }

    private static void ValidateArchiveSize(ZipArchive archive)
    {
        if (archive.Entries.Count > 30000)
            throw new InvalidDataException("El archivo contiene demasiadas entradas.");
        var total = archive.Entries.Sum(e => e.Length);
        if (total > 2L * 1024 * 1024 * 1024)
            throw new InvalidDataException("El contenido descomprimido supera el límite de 2 GB.");
    }

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed class PackMeta
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string Uuid { get; init; }
        public required string Version { get; init; }
        public required AddonCategory Category { get; init; }
        public required List<string> ModuleTypes { get; init; }
        public string? FormatVersion { get; init; }
    }
}
