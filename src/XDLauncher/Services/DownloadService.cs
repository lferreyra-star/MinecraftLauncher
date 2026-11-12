using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace XdLauncher.Services;

public sealed record DownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? Percent => TotalBytes is > 0
        ? Math.Clamp(BytesReceived * 100d / TotalBytes.Value, 0, 100)
        : null;
}

/// <summary>Downloads official Google/GitHub artifacts and extracts only the expected folders.</summary>
public static class DownloadService
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("XDLauncher/1.2 (+https://www.minecraft.net)");
        return client;
    }

    public static async Task DownloadAsync(
        string url,
        string destination,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None,
            1024 * 128, useAsync: true);

        var buffer = new byte[1024 * 128];
        long received = 0;
        var lastReport = DateTime.UtcNow;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
                break;
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;

            if (progress is not null && (DateTime.UtcNow - lastReport).TotalMilliseconds >= 120)
            {
                progress.Report(new DownloadProgress(received, total));
                lastReport = DateTime.UtcNow;
            }
        }

        progress?.Report(new DownloadProgress(received, total ?? received));
    }

    public static async Task<string?> TryDownloadTextAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Extracts a single top-level folder and copies its content to <paramref name="destination"/>.</summary>
    public static void ExtractFolder(string zipPath, string folderName, string destination)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var prefix = folderName.TrimEnd('/', '\\') + "/";
        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entries.Count == 0)
            throw new InvalidOperationException($"El archivo no contiene la carpeta '{folderName}'.");

        Directory.CreateDirectory(destination);
        foreach (var entry in entries)
        {
            var relative = entry.FullName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relative))
                continue;

            var target = Path.GetFullPath(Path.Combine(destination, relative));
            var root = Path.GetFullPath(destination);
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;

            if (relative.EndsWith(Path.DirectorySeparatorChar))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    public static void ExtractAll(string zipPath, string destination, bool overwrite = true)
    {
        Directory.CreateDirectory(destination);
        ZipFile.ExtractToDirectory(zipPath, destination, overwrite);
    }

    public static async Task<string?> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Copies a folder. Returns false when at least one file could not be replaced.</summary>
    public static bool CopyDirectory(string source, string destination, bool overwrite = true)
    {
        var complete = true;
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            try
            {
                File.Copy(file, target, overwrite);
            }
            catch (IOException)
            {
                // The file is in use (for example adb.exe while the ADB server is running).
                Services.LogService.Warn($"No se pudo reemplazar '{target}' porque está en uso.");
                complete = false;
            }
            catch (UnauthorizedAccessException)
            {
                Services.LogService.Warn($"No se pudo reemplazar '{target}' por falta de permisos.");
                complete = false;
            }
        }

        return complete;
    }
}
