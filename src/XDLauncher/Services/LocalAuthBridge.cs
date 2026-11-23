using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XdLauncher.Services;

/// <summary>Token de Firebase entregado por el navegador al launcher.</summary>
public sealed record GoogleBridgeToken(string IdToken, string? RefreshToken);

/// <summary>
/// Puente local de autenticación con Google para el launcher de escritorio.
///
/// El launcher levanta un servidor HTTP en 127.0.0.1/::1 y abre el navegador del
/// sistema en una página que usa Firebase Authentication (mismo proyecto y misma
/// configuración que la web). La página inicia el flujo OAuth de Firebase y entrega
/// el idToken y el refreshToken al launcher. Cada sesión usa un nonce aleatorio que
/// solo conoce la URL abierta por el launcher, de modo que ningún otro proceso local
/// puede inyectar una sesión en el puente. El idToken se valida después contra el
/// servidor con accounts:lookup: no se acepta nada sin comprobar.
/// </summary>
public sealed class LocalAuthBridge : IDisposable
{
    /// <summary>Puerto preferente para que la URL de retorno sea estable entre intentos.</summary>
    private const int PreferredPort = 54865;
    private const int PortAttempts = 20;
    private const int MaxRequestBytes = 32 * 1024;

    private readonly List<TcpListener> _listeners = new();
    private readonly TaskCompletionSource<GoogleBridgeToken> _tokenSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _session;
    private readonly string _html;
    private Task? _acceptLoop;
    private int _started;
    private bool _disposed;

    public LocalAuthBridge()
    {
        Port = CreateListeners();
        _session = CreateSession();
        _html = BuildHtml(_session);
        LogService.Info($"Puente local de Google preparado en el puerto {Port}.");
    }

    /// <summary>Puerto local asignado para esta sesión.</summary>
    public int Port { get; }

    /// <summary>Nonce de un solo uso que protege los endpoints del puente.</summary>
    internal string Session => _session;

    /// <summary>URL que se abre en el navegador del usuario.</summary>
    public string StartUrl =>
        $"http://localhost:{Port}/?start=1&session={Uri.EscapeDataString(_session)}";

    /// <summary>Etapa actual del flujo, tal y como la reporta la página del navegador.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// Abre el navegador (opcionalmente) y espera a que la página local entregue la
    /// sesión de Firebase. La espera termina siempre: con el token, con un error, con
    /// cancelación o con <see cref="TimeoutException"/>. Nunca queda colgada.
    /// </summary>
    public async Task<GoogleBridgeToken> RequestGoogleTokenAsync(TimeSpan timeout,
        CancellationToken cancellationToken = default, bool openBrowser = true)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        if (Interlocked.Exchange(ref _started, 1) == 1)
            throw new InvalidOperationException("El puente de Google ya se está usando.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        StartAcceptLoop(linked.Token);

        using var registration = linked.Token.Register(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _tokenSource.TrySetCanceled(cancellationToken);
                return;
            }

            _tokenSource.TrySetException(new TimeoutException(
                "Se agotó el tiempo para completar el acceso con Google. " +
                "El launcher dejó de esperar para no quedarse bloqueado."));
        });

        if (cancellationToken.IsCancellationRequested)
        {
            _tokenSource.TrySetCanceled(cancellationToken);
            return await _tokenSource.Task.ConfigureAwait(false);
        }

        if (openBrowser)
        {
            LogService.Info($"Puente local de Google escuchando en {StartUrl}.");
            ReportStatus("Abriendo el navegador del sistema…");
            try
            {
                var browserOverride = Environment.GetEnvironmentVariable("XD_AUTH_BROWSER");
                if (!string.IsNullOrWhiteSpace(browserOverride) && File.Exists(browserOverride))
                {
                    LogService.Info($"Usando navegador configurado para Google: {browserOverride}.");
                    Process.Start(new ProcessStartInfo(browserOverride, StartUrl) { UseShellExecute = false });
                }
                else
                {
                    Process.Start(new ProcessStartInfo(StartUrl) { UseShellExecute = true });
                }

                ReportStatus("Esperando tu confirmación en el navegador…");
            }
            catch (Exception ex)
            {
                _tokenSource.TrySetException(new InvalidOperationException(
                    "No se pudo abrir el navegador para acceder con Google. " +
                    "Comprueba que Windows tenga un navegador predeterminado.", ex));
            }
        }

        return await _tokenSource.Task.ConfigureAwait(false);
    }

    /// <summary>Detiene la espera en curso y libera el puerto local.</summary>
    public void Cancel()
    {
        try
        {
            _lifetime.Cancel();
        }
        catch
        {
            // ignore
        }

        _tokenSource.TrySetCanceled();
    }

    private static string CreateSession()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private int CreateListeners()
    {
        for (var port = PreferredPort; port < PreferredPort + PortAttempts; port++)
        {
            var listeners = new List<TcpListener>(2);
            var ipv4 = TryListen(IPAddress.Loopback, port);
            if (ipv4 is not null)
                listeners.Add(ipv4);

            if (Socket.OSSupportsIPv6)
            {
                var ipv6 = TryListen(IPAddress.IPv6Loopback, port);
                if (ipv6 is not null)
                    listeners.Add(ipv6);
            }

            if (listeners.Count == 0)
                continue;

            _listeners.Clear();
            _listeners.AddRange(listeners);
            LogService.Info("Puente local de Google: " +
                string.Join(" + ", _listeners.Select(l => l.LocalEndpoint)) + ".");
            return port;
        }

        // Ningún puerto preferente estaba libre: se usa uno efímero en IPv4 y, si se
        // puede, el mismo en IPv6. La URL de retorno siempre es la que abrimos.
        var fallback4 = new TcpListener(IPAddress.Loopback, 0);
        fallback4.Start();
        var chosen = ((IPEndPoint)fallback4.LocalEndpoint).Port;
        _listeners.Add(fallback4);

        if (Socket.OSSupportsIPv6)
        {
            var fallback6 = TryListen(IPAddress.IPv6Loopback, chosen);
            if (fallback6 is not null)
                _listeners.Add(fallback6);
        }

        return chosen;
    }

    private static TcpListener? TryListen(IPAddress address, int port)
    {
        try
        {
            var listener = new TcpListener(address, port);
            listener.Start();
            return listener;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private void StartAcceptLoop(CancellationToken cancellationToken)
        => _acceptLoop ??= Task.WhenAll(_listeners.Select(listener =>
            AcceptLoopAsync(listener, cancellationToken)));

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 10_000;
                client.SendTimeout = 10_000;
                var stream = client.GetStream();
                var request = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
                if (request is null)
                    return;

                var (method, path, body) = ParseRequest(request);

                if (!IsLoopback(client))
                {
                    await WriteResponseAsync(stream, 403, "text/plain; charset=utf-8", "forbidden")
                        .ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteResponseAsync(stream, 204, "text/plain; charset=utf-8", string.Empty)
                        .ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
                {
                    if (!method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteResponseAsync(stream, 405, "text/plain; charset=utf-8", "method not allowed")
                            .ConfigureAwait(false);
                        return;
                    }

                    await WriteResponseAsync(stream, 200, "text/plain; charset=utf-8", "ok")
                        .ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                        !method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteResponseAsync(stream, 405, "text/plain; charset=utf-8", "method not allowed")
                            .ConfigureAwait(false);
                        return;
                    }

                    LogService.Info("Puente local de Google: página de acceso servida.");
                    await WriteResponseAsync(stream, 200, "text/html; charset=utf-8",
                        method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) ? string.Empty : _html)
                        .ConfigureAwait(false);
                    return;
                }

                if (!path.Equals("/status", StringComparison.OrdinalIgnoreCase) &&
                    !path.Equals("/token", StringComparison.OrdinalIgnoreCase) &&
                    !path.Equals("/error", StringComparison.OrdinalIgnoreCase) &&
                    !path.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteResponseAsync(stream, 404, "text/plain; charset=utf-8", "not found")
                        .ConfigureAwait(false);
                    return;
                }

                if (!method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteResponseAsync(stream, 405, "text/plain; charset=utf-8", "method not allowed")
                        .ConfigureAwait(false);
                    return;
                }

                if (!IsSessionValid(ReadJsonString(body, "session")))
                {
                    LogService.Warn("Puente local de Google: petición rechazada por nonce inválido.");
                    await WriteResponseAsync(stream, 403, "text/plain; charset=utf-8", "invalid session")
                        .ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/status", StringComparison.OrdinalIgnoreCase))
                {
                    var message = ReadJsonString(body, "message") ?? "Progreso del acceso.";
                    var stage = ReadJsonString(body, "stage") ?? "status";
                    if (!_tokenSource.Task.IsCompleted)
                    {
                        LogService.Info($"Puente local de Google · {stage}: {message}");
                        ReportStatus(message);
                    }

                    await WriteResponseAsync(stream, 204, "text/plain; charset=utf-8", string.Empty)
                        .ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/token", StringComparison.OrdinalIgnoreCase))
                {
                    if (_tokenSource.Task.IsCompletedSuccessfully)
                    {
                        await WriteResponseAsync(stream, 200, "text/html; charset=utf-8",
                            ResultPage("Sesión ya transferida",
                                "El launcher ya recibió esta sesión. Puedes cerrar esta pestaña.", true))
                            .ConfigureAwait(false);
                        return;
                    }

                    if (_tokenSource.Task.IsCompleted)
                    {
                        await WriteResponseAsync(stream, 409, "text/html; charset=utf-8",
                            ResultPage("El launcher dejó de esperar",
                                "La ventana de acceso ya no está activa. Vuelve a XD Launcher y pulsa «Reintentar con Google».",
                                false))
                            .ConfigureAwait(false);
                        return;
                    }

                    var token = ReadJsonString(body, "token");
                    var refreshToken = ReadJsonString(body, "refreshToken");
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        await WriteResponseAsync(stream, 400, "text/plain; charset=utf-8", "missing token")
                            .ConfigureAwait(false);
                        return;
                    }

                    LogService.Info("Puente local de Google: sesión de Firebase recibida del navegador.");
                    _tokenSource.TrySetResult(new GoogleBridgeToken(token, refreshToken));
                    await WriteResponseAsync(stream, 200, "text/html; charset=utf-8",
                        ResultPage("Sesión iniciada",
                            "La sesión ya se transfirió al launcher. Puedes cerrar esta pestaña.", true))
                        .ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/error", StringComparison.OrdinalIgnoreCase))
                {
                    var message = ReadJsonString(body, "message") ?? "No se pudo completar el acceso con Google.";
                    LogService.Warn("Puente local de Google: el navegador devolvió un error: " + message);
                    if (!_tokenSource.Task.IsCompleted)
                        _tokenSource.TrySetException(new InvalidOperationException(message));

                    await WriteResponseAsync(stream, 204, "text/plain; charset=utf-8", string.Empty)
                        .ConfigureAwait(false);
                    return;
                }

                LogService.Info("Puente local de Google: el usuario canceló el acceso desde el navegador.");
                if (!_tokenSource.Task.IsCompleted)
                    _tokenSource.TrySetCanceled();

                await WriteResponseAsync(stream, 200, "text/html; charset=utf-8",
                    ResultPage("Acceso cancelado",
                        "Ya puedes cerrar esta pestaña y volver al launcher.", false))
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // El flujo se canceló o terminó: no hay nada que registrar.
        }
        catch (Exception ex)
        {
            LogService.Warn("Error en el puente local de autenticación: " + ex.Message);
        }
    }

    private bool IsSessionValid(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var expected = Encoding.UTF8.GetBytes(_session);
        var supplied = Encoding.UTF8.GetBytes(candidate);
        return expected.Length == supplied.Length &&
               CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private static bool IsLoopback(TcpClient client)
    {
        try
        {
            return client.Client.RemoteEndPoint is IPEndPoint endPoint && IPAddress.IsLoopback(endPoint.Address);
        }
        catch
        {
            return false;
        }
    }

    private void ReportStatus(string message)
    {
        try
        {
            StatusChanged?.Invoke(message);
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo notificar el progreso del puente de Google: " + ex.Message);
        }
    }

    private static async Task<string?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxRequestBytes];
        var total = 0;

        while (total < buffer.Length)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                return null;
            }

            if (read <= 0)
                break;

            total += read;

            var text = Encoding.UTF8.GetString(buffer, 0, total);
            var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
                continue;

            var contentLength = 0;
            foreach (var line in text[..headerEnd].Split("\r\n"))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line["Content-Length:".Length..].Trim(), out var parsed))
                {
                    contentLength = parsed;
                }
            }

            if (total >= headerEnd + 4 + contentLength)
                return text;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static (string Method, string Path, string Body) ParseRequest(string request)
    {
        var lines = request.Split("\r\n");
        var first = lines.Length > 0 ? lines[0].Split(' ') : Array.Empty<string>();
        var method = first.Length > 0 ? first[0] : "GET";
        var path = first.Length > 1 ? first[1] : "/";

        var separator = path.IndexOf('?');
        if (separator >= 0)
            path = path[..separator];

        var headerEnd = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var body = headerEnd >= 0 ? request[(headerEnd + 4)..] : string.Empty;
        return (method, path, body);
    }

    private static string? ReadJsonString(string json, string key)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(key, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string contentType, string body)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var reason = status switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            409 => "Conflict",
            500 => "Internal Server Error",
            _ => "OK"
        };

        var header = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {payload.Length}\r\n" +
            "Cache-Control: no-store\r\nPragma: no-cache\r\nX-Content-Type-Options: nosniff\r\n" +
            "Referrer-Policy: no-referrer\r\nConnection: close\r\n\r\n");

        await stream.WriteAsync(header).ConfigureAwait(false);
        if (payload.Length > 0)
            await stream.WriteAsync(payload).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static string ResultPage(string heading, string message, bool success) =>
        "<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        "<meta name=\"referrer\" content=\"no-referrer\"><title>XD Launcher</title></head>" +
        "<body style=\"font-family:Segoe UI,system-ui,sans-serif;background:" +
        "radial-gradient(900px 500px at 20% 0%,#142a1d 0%,transparent 60%)," +
        "radial-gradient(800px 500px at 80% 100%,#12243a 0%,transparent 60%),#0d1117;color:#f0f6fc;" +
        "display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;padding:24px\">" +
        "<div style=\"max-width:470px;width:100%;padding:38px 32px;border-radius:22px;text-align:center;" +
        "background:linear-gradient(180deg,rgba(23,29,41,.97),rgba(15,19,27,.97));border:1px solid #2b3444;" +
        "box-shadow:0 30px 80px rgba(0,0,0,.55)\">" +
        "<div style=\"width:64px;height:64px;margin:0 auto 18px;border-radius:20px;" +
        (success
            ? "background:rgba(92,224,122,.12);border:1px solid rgba(92,224,122,.45);color:#5ce07a;"
            : "background:rgba(248,113,113,.1);border:1px solid rgba(248,113,113,.4);color:#f87171;") +
        "display:flex;align-items:center;justify-content:center;font-size:28px;font-weight:700;" +
        "box-shadow:0 0 50px " + (success ? "rgba(92,224,122,.22)" : "rgba(248,113,113,.16)") + "\">" +
        (success ? "&#10003;" : "!") + "</div>" +
        $"<h1 style=\"margin:0 0 10px;font-size:1.28rem\">{WebUtility.HtmlEncode(heading)}</h1>" +
        $"<p style=\"margin:0;color:#9aa7b8;line-height:1.6;font-size:.94rem\">{WebUtility.HtmlEncode(message)}</p>" +
        "<p style=\"margin:22px 0 0;color:#64748b;font-size:.78rem\">XD Launcher · Firebase Authentication</p>" +
        "</div></body></html>";

    /// <summary>
    /// Página local que delega en Firebase Authentication el inicio de sesión con
    /// Google y devuelve la sesión al launcher. Usa popup como mecanismo principal y
    /// redirección solo como respaldo. Todos los envíos incluyen el nonce de sesión.
    /// </summary>
    private static string BuildHtml(string session)
    {
        var token = JsonSerializer.Serialize(session);
        return """
        <!doctype html>
        <html lang="es">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="referrer" content="no-referrer">
        <title>XD Launcher · Acceso con Google</title>
        <style>
          :root { color-scheme: dark; }
          * { box-sizing: border-box; }
          body { margin:0; min-height:100vh; display:flex; align-items:center; justify-content:center;
                 background:radial-gradient(900px 500px at 18% 0%, #142a1d 0%, transparent 60%),
                            radial-gradient(760px 520px at 82% 100%, #12243a 0%, transparent 62%), #0d1117;
                 color:#f0f6fc; font-family:"Segoe UI",system-ui,sans-serif; overflow:hidden; padding:24px; }
          .orb { position:fixed; border-radius:50%; filter:blur(70px); pointer-events:none; opacity:.5; }
          .orb.a { width:420px; height:420px; left:-160px; top:-180px; background:#1d7a48;
                   animation:drift 18s ease-in-out infinite alternate; }
          .orb.b { width:380px; height:380px; right:-150px; bottom:-160px; background:#125a8a;
                   animation:drift 22s ease-in-out infinite alternate-reverse; }
          @keyframes drift { from { transform:translate3d(-16px,8px,0) } to { transform:translate3d(26px,-22px,0) } }
          .card { position:relative; width:min(480px,100%); padding:38px 34px 30px; border-radius:24px;
                  background:linear-gradient(180deg,rgba(23,29,41,.97),rgba(14,18,26,.98));
                  border:1px solid #2b3444; box-shadow:0 34px 90px rgba(0,0,0,.6); text-align:center; }
          .card::before { content:""; position:absolute; inset:0 18% auto; height:1px;
                          background:linear-gradient(90deg,transparent,rgba(92,224,122,.65),rgba(56,189,248,.55),transparent); }
          .logo { width:76px; height:76px; margin:0 auto 20px; border-radius:22px; background:#16241c;
                  border:1px solid #2f5a3d; display:flex; align-items:center; justify-content:center;
                  box-shadow:0 0 44px rgba(92,224,122,.24); }
          .logo img { width:54px; height:54px; object-fit:contain; }
          .badge { display:inline-flex; align-items:center; gap:7px; padding:5px 10px; border-radius:999px;
                   border:1px solid rgba(92,224,122,.24); background:rgba(92,224,122,.07);
                   color:#9fe9b2; font-size:10px; font-weight:700; letter-spacing:.12em; text-transform:uppercase; }
          h1 { font-size:1.34rem; margin:16px 0 8px; letter-spacing:-.01em; }
          p { color:#9aa7b8; font-size:.92rem; margin:0 0 20px; line-height:1.6; }
          .bar { height:4px; border-radius:3px; background:#1e2735; overflow:hidden; margin:0 0 20px; }
          .bar span { display:block; height:100%; width:38%; border-radius:3px;
                      background:linear-gradient(90deg,#5ce07a,#38bdf8); animation:slide 1.15s ease-in-out infinite; }
          @keyframes slide { 0% { margin-left:-38%; } 100% { margin-left:100%; } }
          .btn { width:100%; border:0; border-radius:13px; padding:14px 18px; margin-top:10px;
                 font-family:inherit; font-size:.9rem; font-weight:700; letter-spacing:.01em;
                 cursor:pointer; transition:filter .15s ease, opacity .15s ease, transform .15s ease;
                 background:linear-gradient(135deg,#5ce07a,#35c25b); color:#06210f;
                 box-shadow:0 14px 34px rgba(92,224,122,.2); }
          .btn.google { display:flex; align-items:center; justify-content:center; gap:11px;
                        background:#ffffff; color:#1f2937; border:1px solid #e5e7eb;
                        box-shadow:0 16px 38px rgba(0,0,0,.32); }
          .btn.google svg { width:20px; height:20px; flex:none; }
          .btn:hover { filter:brightness(1.05); transform:translateY(-1px); }
          .btn:active { transform:translateY(0); }
          .btn:disabled { opacity:.55; cursor:default; transform:none; }
          .link { background:none; border:0; color:#8b98ab; font-family:inherit; font-size:.8rem;
                  cursor:pointer; text-decoration:none; margin-top:14px; padding:7px; }
          .link:hover { color:#c9d3e0; text-decoration:underline; }
          .ok { color:#5ce07a; font-weight:600; }
          .err { color:#f87171; font-weight:600; }
          .hint { display:block; margin-top:10px; color:#77859a; font-size:.79rem; line-height:1.55; }
          .steps { display:flex; justify-content:center; gap:8px; margin:18px 0 0; }
          .step { width:26px; height:3px; border-radius:2px; background:#243043; }
          .step.on { background:linear-gradient(90deg,#5ce07a,#38bdf8); }
          .done { width:78px; height:78px; margin:2px auto 18px; border-radius:50%;
                  display:flex; align-items:center; justify-content:center; font-size:34px; font-weight:700;
                  color:#06210f; background:linear-gradient(135deg,#5ce07a,#35c25b);
                  box-shadow:0 0 54px rgba(92,224,122,.35); }
          .hidden { display:none !important; }
          .footer { margin-top:20px; color:#5f6b7b; font-size:.72rem; letter-spacing:.08em; text-transform:uppercase; }
          @media (prefers-reduced-motion: reduce) {
            .orb { animation:none !important; }
            .bar span { animation-duration:2.8s; }
            .btn { transition:none; }
          }
        </style>
        </head>
        <body>
          <div class="orb a"></div><div class="orb b"></div>
          <main class="card" role="main">
            <div class="logo"><img src="https://xdluncher.gt.tc/assets/logo.png" alt="XD Launcher"
                 onerror="this.style.display='none';this.parentElement.textContent='XD';this.parentElement.style.fontSize='24px';this.parentElement.style.fontWeight='700';this.parentElement.style.color='#5ce07a';"></div>
            <span class="badge" id="badge">ACCESO SEGURO</span>
            <h1 id="title">Acceso con Google</h1>
            <p id="msg">Preparando el acceso seguro con Google…</p>
            <div class="bar hidden" id="bar"><span></span></div>
            <div class="steps" id="steps"><span class="step on"></span><span class="step"></span><span class="step"></span></div>
            <button class="btn google hidden" id="retry" type="button">
              <svg viewBox="0 0 48 48" aria-hidden="true" focusable="false">
                <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/>
                <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/>
                <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"/>
                <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/>
              </svg>
              <span>CONTINUAR CON GOOGLE</span>
            </button>
            <button class="link hidden" id="cancel" type="button">Cancelar y volver al launcher</button>
            <button class="link hidden" id="close" type="button">Cerrar esta pestaña</button>
            <div class="footer">Conexión local protegida · Firebase Authentication</div>
          </main>
        <script type="module">
          const cfg = {
            apiKey: "AIzaSyDegbqMJdTeNvkK17z4tRUW2490yV8FWXE",
            authDomain: "xd-launcher-8082d.firebaseapp.com",
            projectId: "xd-launcher-8082d",
            storageBucket: "xd-launcher-8082d.firebasestorage.app",
            messagingSenderId: "463172269791",
            appId: "1:463172269791:web:81ccbd0d79ba22a9b9db6a"
          };

          const SESSION = __SESSION__;
          const START_KEY = 'xd_google_started';
          const title = document.getElementById('title');
          const msg = document.getElementById('msg');
          const bar = document.getElementById('bar');
          const steps = document.getElementById('steps');
          const badge = document.getElementById('badge');
          const retry = document.getElementById('retry');
          const cancel = document.getElementById('cancel');
          const close = document.getElementById('close');

          const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
          const readStored = () => { try { return sessionStorage.getItem('xd_bridge_session'); } catch { return null; } };
          const writeStored = (value) => { try { if (value) sessionStorage.setItem('xd_bridge_session', value); } catch { } };
          const clearStored = () => { try { sessionStorage.removeItem('xd_bridge_session'); sessionStorage.removeItem(START_KEY); } catch { } };

          const bridgeSession = new URLSearchParams(location.search).get('session') || readStored() || SESSION || '';
          writeStored(bridgeSession);

          let auth = null;
          let firebaseAuth = null;
          let sent = false;
          let started = false;
          let finished = false;
          let delivering = false;
          let leaving = false;
          let watchdog = null;

          const post = async (path, payload = {}) => {
            if (!bridgeSession) return false;
            try {
              const response = await fetch(path, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(Object.assign({ session: bridgeSession }, payload)),
                keepalive: true
              });
              return response.ok;
            } catch {
              return false;
            }
          };

          const beacon = (path, payload = {}) => {
            if (!bridgeSession) return false;
            try {
              const blob = new Blob([JSON.stringify(Object.assign({ session: bridgeSession }, payload))],
                { type: 'application/json' });
              return navigator.sendBeacon(path, blob);
            } catch {
              return false;
            }
          };

          const report = (stage, message) => beacon('/status', { stage: stage, message: message });
          const notifyError = (message) => { if (!finished) beacon('/error', { message: message }); };
          const setStep = (index) => {
            if (!steps) return;
            [...steps.children].forEach((node, i) => node.classList.toggle('on', i <= index));
          };
          const clearWatchdog = () => { if (watchdog) { clearTimeout(watchdog); watchdog = null; } };
          const armWatchdog = () => {
            clearWatchdog();
            watchdog = setTimeout(() => {
              if (finished || delivering || leaving) return;
              showError(new Error('El acceso tardó demasiado. Vuelve al launcher y pulsa «Reintentar con Google».'),
                true, false);
            }, 210000);
          };

          const escapeHtml = (value) => String(value)
            .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;').replaceAll("'", '&#39;');

          const friendlyError = (error) => {
            const code = error && error.code ? error.code : '';
            switch (code) {
              case 'auth/unauthorized-domain':
                return 'El dominio localhost no está autorizado en Firebase. Agrégalo en Firebase Console > Authentication > Settings > Authorized domains y vuelve a intentarlo.';
              case 'auth/operation-not-allowed':
                return 'El proveedor de Google no está habilitado. Actívalo en Firebase Console > Authentication > Sign-in method > Google.';
              case 'auth/network-request-failed':
                return 'No hay conexión con el servidor de autenticación. Revisa tu red y vuelve a intentarlo.';
              case 'auth/account-exists-with-different-credential':
                return 'Ese correo ya usa otro método de acceso. Vuelve al launcher y entra con correo y contraseña.';
              case 'auth/user-disabled':
                return 'Esta cuenta está deshabilitada en XD Launcher.';
              case 'auth/internal-error':
                return 'Firebase devolvió un error interno. Pulsa «Reintentar con Google» o usa correo y contraseña en el launcher.';
              case 'auth/web-storage-unsupported':
                return 'El navegador bloquea el almacenamiento necesario para el acceso. Permite cookies y almacenamiento local para localhost.';
              case 'auth/cancelled-popup-request':
              case 'auth/user-cancelled':
                return 'Se canceló el acceso con Google. Puedes volver a intentarlo.';
              default:
                return (error && error.message) ? error.message : 'No se pudo completar el acceso con Google.';
            }
          };

          const showBusy = (heading, text, step) => {
            clearWatchdog();
            setStep(step);
            title.textContent = heading;
            msg.textContent = text;
            badge.textContent = 'CONECTANDO';
            bar.classList.remove('hidden');
            steps.classList.remove('hidden');
            retry.classList.add('hidden');
            retry.disabled = true;
            cancel.classList.remove('hidden');
            close.classList.add('hidden');
          };

          const showReady = (text) => {
            setStep(0);
            title.textContent = 'Acceso con Google';
            msg.textContent = text || 'Pulsa el botón para iniciar sesión con tu cuenta de Google.';
            badge.textContent = 'ACCESO SEGURO';
            bar.classList.add('hidden');
            steps.classList.remove('hidden');
            retry.classList.remove('hidden');
            retry.disabled = false;
            cancel.classList.remove('hidden');
            close.classList.add('hidden');
            armWatchdog();
          };

          const showSuccess = () => {
            finished = true;
            clearWatchdog();
            clearStored();
            setStep(3);
            title.textContent = 'Sesión iniciada';
            msg.innerHTML = '<span class="ok">Sesión transferida al launcher.</span>'
              + '<span class="hint">Ya puedes cerrar esta pestaña y volver a XD Launcher.</span>';
            badge.textContent = 'LISTO';
            bar.classList.add('hidden');
            steps.classList.add('hidden');
            retry.classList.add('hidden');
            cancel.classList.add('hidden');
            close.classList.remove('hidden');
          };

          const showError = (error, notify = true, canRetry = true) => {
            if (finished) return;
            clearWatchdog();
            const message = friendlyError(error);
            title.textContent = 'No se pudo iniciar sesión';
            msg.innerHTML = '<span class="err">' + escapeHtml(message) + '</span>'
              + '<span class="hint">Vuelve al launcher para reintentar con la ventana emergente o usa correo y contraseña.</span>';
            badge.textContent = 'ERROR';
            bar.classList.add('hidden');
            steps.classList.add('hidden');
            retry.classList.toggle('hidden', !canRetry);
            retry.disabled = !canRetry;
            cancel.classList.remove('hidden');
            started = false;
            if (notify) notifyError(message);
            else report('error', message);
          };

          const sendToken = async (user) => {
            if (sent || finished) return;
            sent = true;
            delivering = true;
            showBusy('Sesión iniciada', 'Transfiriendo la sesión al launcher…', 2);
            report('delivering', 'Transfiriendo la sesión al launcher…');
            try {
              if (!bridgeSession) throw new Error('La página no tiene una clave de sesión válida. Vuelve al launcher y reintenta.');
              const idToken = await user.getIdToken(true);
              if (!idToken) throw new Error('Firebase no devolvió un token de sesión.');
              const refreshToken = user.refreshToken || '';
              const ok = await post('/token', { token: idToken, refreshToken: refreshToken });
              if (!ok) throw new Error('No se pudo entregar la sesión al launcher. Vuelve al launcher y reintenta el acceso.');
              delivering = false;
              showSuccess();
            } catch (error) {
              sent = false;
              delivering = false;
              finished = false;
              showError(error, true, false);
            }
          };

          const start = async () => {
            if (started || sent || finished || !firebaseAuth || !auth) return;
            started = true;
            retry.disabled = true;
            showBusy('Conectando con Google…', 'Se abrirá una ventana de Google. Completa el acceso y vuelve a esta pestaña.', 1);
            report('popup', 'Abriendo la ventana de Google…');

            const provider = new firebaseAuth.GoogleAuthProvider();
            provider.setCustomParameters({ prompt: 'select_account' });

            try {
              const result = await firebaseAuth.signInWithPopup(auth, provider);
              clearStored();
              if (result && result.user) {
                await sendToken(result.user);
                return;
              }
              started = false;
              showError(new Error('Google no devolvió una sesión completada. Vuelve a pulsar «Continuar con Google».'));
            } catch (error) {
              const code = error && error.code ? error.code : '';
              if (code === 'auth/popup-closed-by-user' || code === 'auth/cancelled-popup-request') {
                started = false;
                showReady('Se cerró la ventana de Google. Pulsa «Continuar con Google» para volver a intentarlo.');
                report('ready', 'Ventana de Google cerrada. Pulsa «Continuar con Google» para reintentar.');
                return;
              }

              if (code === 'auth/popup-blocked' || code === 'auth/operation-not-supported-in-this-environment') {
                try {
                  sessionStorage.setItem(START_KEY, '1');
                  leaving = true;
                  showBusy('Abriendo Google…', 'La ventana emergente fue bloqueada; continuamos en esta pestaña.', 1);
                  report('redirecting', 'Abriendo la página de Google…');
                  await firebaseAuth.signInWithRedirect(auth, provider);
                } catch (redirectError) {
                  leaving = false;
                  clearStored();
                  started = false;
                  showError(redirectError);
                }
                return;
              }

              clearStored();
              started = false;
              showError(error);
            }
          };

          const waitForUser = async (milliseconds) => {
            if (auth && auth.currentUser) return auth.currentUser;
            return await new Promise((resolve) => {
              let done = false;
              const finish = (user) => {
                if (done) return;
                done = true;
                try { unsubscribe(); } catch { }
                clearTimeout(timer);
                resolve(user || null);
              };
              const unsubscribe = firebaseAuth.onAuthStateChanged(auth, (user) => {
                if (user) finish(user);
              });
              const timer = setTimeout(() => finish(auth ? auth.currentUser : null), milliseconds);
            });
          };

          const recoverRedirect = async () => {
            const params = new URLSearchParams(location.search);
            const hash = new URLSearchParams(location.hash.replace(/^#/, ''));
            const hasParams = ['apiKey', 'eventId', 'state', 'code', 'authType'].some((key) =>
              params.has(key) || hash.has(key));

            if (!hasParams) {
              clearStored();
              showError(new Error('La redirección de Google volvió sin una sesión completada. Pulsa «Continuar con Google» para usar la ventana emergente.'),
                false, true);
              report('ready', 'La redirección no completó la sesión. Pulsa «Continuar con Google» para reintentar.');
              return;
            }

            showBusy('Recuperando tu sesión…', 'Estamos confirmando el acceso con Google.', 1);
            report('returned', 'Recuperando la respuesta de Google…');
            let redirectError = null;
            let user = null;

            try {
              user = await Promise.race([
                firebaseAuth.getRedirectResult(auth)
                  .then((result) => (result && result.user) ? result.user : null)
                  .catch((error) => { redirectError = error; return null; }),
                delay(20000).then(() => null)
              ]);
            } catch (error) {
              redirectError = error;
            }

            if (!user && !redirectError) user = await waitForUser(10000);

            if (user) {
              clearStored();
              await sendToken(user);
              return;
            }

            clearStored();
            showError(redirectError || new Error('Google no devolvió una sesión completada. Pulsa «Continuar con Google» para usar la ventana emergente.'),
              false, true);
            report('ready', 'La redirección no completó la sesión. Pulsa «Continuar con Google» para reintentar.');
          };

          cancel.addEventListener('click', async () => {
            if (finished || delivering) return;
            finished = true;
            clearWatchdog();
            await post('/cancel', {});
            title.textContent = 'Acceso cancelado';
            msg.textContent = 'Puedes cerrar esta pestaña y volver al launcher.';
            badge.textContent = 'CANCELADO';
            bar.classList.add('hidden');
            steps.classList.add('hidden');
            retry.classList.add('hidden');
            cancel.classList.add('hidden');
            close.classList.remove('hidden');
          });

          close.addEventListener('click', () => window.close());
          retry.addEventListener('click', start);
          window.addEventListener('pagehide', () => {
            if (!finished && !delivering && !leaving) beacon('/cancel', {});
          });

          try {
            if (!bridgeSession) {
              showError(new Error('Falta la clave de sesión del launcher. Abre el acceso desde XD Launcher.'), false, false);
            } else {
              const appModule = await import('https://www.gstatic.com/firebasejs/10.12.5/firebase-app.js');
              const authModule = await import('https://www.gstatic.com/firebasejs/10.12.5/firebase-auth.js');

              firebaseAuth = authModule;
              auth = authModule.getAuth(appModule.initializeApp(cfg));
              try { await authModule.setPersistence(auth, authModule.browserLocalPersistence); } catch { }

              report('loaded', 'Página de acceso preparada.');

              const params = new URLSearchParams(location.search);
              const hash = new URLSearchParams(location.hash.replace(/^#/, ''));
              const hasRedirectParams = ['apiKey', 'eventId', 'state', 'code', 'authType'].some((key) =>
                params.has(key) || hash.has(key));
              const returning = hasRedirectParams || sessionStorage.getItem(START_KEY) === '1';

              if (returning) {
                await recoverRedirect();
              } else {
                showReady('Pulsa «Continuar con Google» para iniciar sesión con tu cuenta.');
                report('ready', 'Pulsa «Continuar con Google» en la página del navegador para iniciar sesión.');
              }
            }
          } catch (error) {
            const message = 'No se pudo cargar el sistema de acceso de Google. '
              + 'Revisa tu conexión a Internet o desactiva el bloqueador de contenido y vuelve a intentarlo.';
            showError(new Error(message + ' (' + friendlyError(error) + ')'), true, false);
          }
        </script>
        </body>
        </html>
        """.Replace("__SESSION__", token);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _lifetime.Cancel();
        }
        catch
        {
            // ignore
        }

        foreach (var listener in _listeners)
        {
            try
            {
                listener.Stop();
            }
            catch
            {
                // ignore
            }
        }

        _listeners.Clear();
        _lifetime.Dispose();
    }
}
