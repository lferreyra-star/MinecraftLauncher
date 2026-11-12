using System.Diagnostics;
using System.Text;

namespace XdLauncher.Services;

public sealed class ProcessResult
{
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = string.Empty;
    public string StdErr { get; init; } = string.Empty;
    public bool TimedOut { get; init; }

    public bool Success => !TimedOut && ExitCode == 0;
    public string Combined => string.Join(Environment.NewLine,
        new[] { StdOut, StdErr }.Where(v => !string.IsNullOrWhiteSpace(v)));
}

/// <summary>Runs console tools (adb, scrcpy, sdkmanager) without showing a window.</summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        string? standardInput = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("El ejecutable no puede estar vacío.", nameof(fileName));

        var (actualFile, actualArguments) = WrapIfBatch(fileName, arguments);
        var startInfo = new ProcessStartInfo
        {
            FileName = actualFile,
            Arguments = actualArguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return new ProcessResult { ExitCode = -1, StdErr = "No se pudo iniciar el proceso." };
        }
        catch (Exception ex)
        {
            return new ProcessResult { ExitCode = -1, StdErr = ex.Message };
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        if (standardInput is not null)
        {
            try
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
                await process.StandardInput.FlushAsync(cancellationToken);
            }
            catch
            {
                // The process may exit before consuming all input.
            }
            finally
            {
                try { process.StandardInput.Close(); } catch { /* ignore */ }
            }
        }

        CancellationTokenSource? timeoutCts = null;
        if (timeout is { } value)
        {
            timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(value);
        }
        using var timeoutScope = timeoutCts;

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }
        }

        string stdout;
        string stderr;
        try
        {
            stdout = await stdoutTask;
        }
        catch
        {
            stdout = string.Empty;
        }
        try
        {
            stderr = await stderrTask;
        }
        catch
        {
            stderr = string.Empty;
        }

        var exitCode = timedOut ? -2 : process.HasExited ? process.ExitCode : -1;
        return new ProcessResult
        {
            ExitCode = exitCode,
            StdOut = stdout.TrimEnd(),
            StdErr = stderr.TrimEnd(),
            TimedOut = timedOut
        };
    }

    private static (string FileName, string Arguments) WrapIfBatch(string fileName, string arguments)
    {
        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            return (fileName, arguments);

        return ("cmd.exe", $"/d /s /c \"\"{fileName}\" {arguments}\"");
    }
}
