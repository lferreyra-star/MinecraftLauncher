using System.Text;

namespace XdLauncher.Services;

/// <summary>Thread-safe file logger with rotation. Never logs secrets.</summary>
public static class LogService
{
    private static readonly object Gate = new();
    private static string? _logDirectory;
    private static string? _currentFile;
    private static DateOnly _currentDate;

    public static string LogDirectory => _logDirectory ?? throw new InvalidOperationException("LogService no está inicializado.");
    public static string? CurrentFile => _currentFile;
    public static event Action<string>? MessageWritten;

    public static void Initialize(string? directory = null)
    {
        _logDirectory = directory ?? Paths.LogsDirectory;
        Directory.CreateDirectory(_logDirectory);
        _currentDate = DateOnly.FromDateTime(DateTime.Now);
        _currentFile = Path.Combine(_logDirectory, $"launcher-{_currentDate:yyyyMMdd}.log");
        Write("INFO", "===== XD Launcher iniciado =====");
        Write("INFO", $"Versión: {AppInfo.Version} · {Environment.OSVersion.VersionString} · {Environment.Version}");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(Exception exception, string? context = null)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(context))
            builder.Append(context).Append(" → ");
        builder.Append(exception.GetType().Name).Append(": ").AppendLine(exception.Message);
        builder.AppendLine(exception.StackTrace);
        if (exception.InnerException is not null)
            builder.AppendLine($"Inner: {exception.InnerException}");
        Write("ERROR", builder.ToString());
    }

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_logDirectory is not null && (today != _currentDate || _currentFile is null))
                {
                    _currentDate = today;
                    _currentFile = Path.Combine(_logDirectory, $"launcher-{today:yyyyMMdd}.log");
                    Cleanup();
                }

                if (_currentFile is null)
                    return;

                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(_currentFile, line, Encoding.UTF8);
                MessageWritten?.Invoke(line.TrimEnd());
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }

    private static void Cleanup()
    {
        try
        {
            if (_logDirectory is null)
                return;
            foreach (var file in Directory.EnumerateFiles(_logDirectory, "launcher-*.log")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Skip(10))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // ignore
        }
    }
}

public static class AppInfo
{
    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static string FullVersion =>
        typeof(AppInfo).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";

    public const string ProductName = "XD Launcher";
    public const string WindowTitle = "XD Launcher";
    public const string MinecraftProductId = "9NBLGGH2JHXJ";
    public const string Website = "https://www.minecraft.net";
    public const string StoreWebUrl = "https://apps.microsoft.com/detail/9NBLGGH2JHXJ";
}
