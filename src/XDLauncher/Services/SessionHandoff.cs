using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XdLauncher.Services;

/// <summary>
/// Canal local para reenviar el traspaso de sesión (URI xdlauncher://) a la instancia
/// del launcher que ya está ejecutándose. Usa un named pipe con el usuario actual.
/// </summary>
public static class SessionHandoff
{
    public const string Scheme = "xdlauncher";
    private const string PipeName = "XDLauncher.SessionHandoff";

    /// <summary>Registra el esquema xdlauncher:// para el usuario actual (no requiere admin).</summary>
    public static void RegisterProtocol()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{Scheme}\shell\open\command");
            if (key is null)
                return;

            var exe = Path.Combine(AppContext.BaseDirectory, "MinecraftLauncher.exe");
            key.SetValue(null, $"\"{exe}\" \"%1\"");
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo registrar el protocolo xdlauncher://: " + ex.Message);
        }
    }

    /// <summary>Extrae el token de sesión de una URI xdlauncher://auth?token=...</summary>
    public static string? ExtractToken(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri) || !uri.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var query = uri.Contains('?') ? uri[(uri.IndexOf('?') + 1)..] : string.Empty;
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                    continue;

                var key = Uri.UnescapeDataString(pair[..separator]);
                var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
                if (key.Equals("token", StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo interpretar la URI de traspaso: " + ex.Message);
        }

        return null;
    }

    /// <summary>Reenvía la URI a la instancia que ya está en ejecución.</summary>
    public static bool ForwardToRunningInstance(string uri)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            var payload = Encoding.UTF8.GetBytes(uri);
            client.Write(payload, 0, payload.Length);
            client.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Escucha en segundo plano las URI entrantes mientras la app está abierta.</summary>
    public static void StartListener(Action<string> onUri)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    server.WaitForConnection();

                    using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
                    var uri = reader.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(uri))
                    {
                        try
                        {
                            onUri(uri);
                        }
                        catch (Exception ex)
                        {
                            LogService.Error(ex, "Error procesando el traspaso de sesión.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warn("El canal de traspaso de sesión se interrumpió: " + ex.Message);
                    Thread.Sleep(500);
                }
            }
        })
        {
            IsBackground = true,
            Name = "XDLauncher.SessionHandoff"
        };
        thread.Start();
    }
}
