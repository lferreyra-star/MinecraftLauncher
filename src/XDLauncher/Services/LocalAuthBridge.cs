using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XdLauncher.Services;

/// <summary>
/// Puente local de autenticación con Google para el launcher de escritorio.
///
/// El launcher levanta un servidor HTTP en 127.0.0.1 y abre el navegador del sistema
/// en una página mínima que usa Firebase Authentication (mismo proyecto y misma
/// configuración que la web). Al iniciar sesión con Google, el navegador vuelve a la
/// página local, que entrega el idToken de Firebase al launcher. El token se valida
/// después contra el servidor con accounts:lookup, de modo que nunca se acepta nada
/// sin comprobar. No requiere contraseña, ni claves privadas, ni cambios en Firebase.
/// </summary>
public sealed class LocalAuthBridge : IDisposable
{
    private readonly TcpListener _listener;
    private readonly TaskCompletionSource<string> _tokenSource = new();
    private readonly string _html;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public LocalAuthBridge()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _html = BuildHtml();
    }

    /// <summary>Puerto local asignado para esta sesión.</summary>
    public int Port { get; }

    /// <summary>URL que se abre en el navegador del usuario.</summary>
    public string StartUrl => $"http://localhost:{Port}/?start=1";

    /// <summary>
    /// Abre el navegador y espera a que la página local entregue el idToken de Firebase.
    /// </summary>
    public async Task<string> RequestGoogleTokenAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cts.CancelAfter(timeout);

        var acceptTask = AcceptLoopAsync(_cts.Token);

        Process.Start(new ProcessStartInfo(StartUrl) { UseShellExecute = true });

        using var registration = cancellationToken.Register(() => _tokenSource.TrySetCanceled());
        return await _tokenSource.Task;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
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
                var stream = client.GetStream();
                var request = await ReadRequestAsync(stream, cancellationToken);
                if (request is null)
                    return;

                var (path, body) = ParseRequest(request);

                if (path.StartsWith("/token", StringComparison.OrdinalIgnoreCase))
                {
                    var token = ExtractJsonValue(body, "token");
                    if (!string.IsNullOrWhiteSpace(token))
                        _tokenSource.TrySetResult(token);

                    await WriteResponseAsync(stream, 200, "text/html; charset=utf-8",
                        "<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\"><title>XD Launcher</title></head>" +
                        "<body style=\"font-family:Segoe UI,sans-serif;background:#0d1117;color:#f0f6fc;display:flex;align-items:center;justify-content:center;height:100vh;margin:0\">" +
                        "<div style=\"text-align:center\"><h2 style=\"color:#5ce07a\">Sesión iniciada</h2>" +
                        "<p>Ya puedes cerrar esta pestaña y volver al launcher.</p></div></body></html>");
                    return;
                }

                if (path.StartsWith("/error", StringComparison.OrdinalIgnoreCase))
                {
                    var message = ExtractJsonValue(body, "message");
                    if (!string.IsNullOrWhiteSpace(message))
                        _tokenSource.TrySetException(new InvalidOperationException(message));
                }

                await WriteResponseAsync(stream, 200, "text/html; charset=utf-8", _html);
            }
        }
        catch (Exception ex)
        {
            LogService.Warn("Error en el puente local de autenticación: " + ex.Message);
        }
    }

    private static async Task<string?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        var total = 0;

        while (total < buffer.Length)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
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

    private static (string Path, string Body) ParseRequest(string request)
    {
        var lines = request.Split("\r\n");
        var first = lines.Length > 0 ? lines[0].Split(' ') : Array.Empty<string>();
        var path = first.Length > 1 ? first[1] : "/";

        var headerEnd = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var body = headerEnd >= 0 ? request[(headerEnd + 4)..] : string.Empty;
        return (path, body);
    }

    private static string? ExtractJsonValue(string json, string key)
    {
        var marker = $"\"{key}\":";
        var index = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var start = index + marker.Length;
        while (start < json.Length && char.IsWhiteSpace(json[start]))
            start++;

        if (start >= json.Length)
            return null;

        if (json[start] == '"')
        {
            var end = json.IndexOf('"', start + 1);
            return end > start ? json[(start + 1)..end] : null;
        }

        var stop = start;
        while (stop < json.Length && json[stop] != ',' && json[stop] != '}')
            stop++;

        return json[start..stop].Trim();
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string contentType, string body)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var header = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status} OK\r\nContent-Type: {contentType}\r\nContent-Length: {payload.Length}\r\n" +
            "Cache-Control: no-store\r\nConnection: close\r\n\r\n");

        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    /// <summary>
    /// Página local mínima que delega en Firebase Authentication el inicio de sesión con
    /// Google y devuelve el token al launcher.
    /// </summary>
    private static string BuildHtml() =>
        """
        <!doctype html>
        <html lang="es">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>XD Launcher · Acceso con Google</title>
        <style>
          :root { color-scheme: dark; }
          * { box-sizing: border-box; }
          body { margin:0; min-height:100vh; display:flex; align-items:center; justify-content:center;
                 background:radial-gradient(900px 500px at 20% 0%, #142a1d 0%, transparent 60%),
                            radial-gradient(800px 500px at 80% 100%, #12243a 0%, transparent 60%), #0d1117;
                 color:#f0f6fc; font-family:"Segoe UI",system-ui,sans-serif; }
          .card { width:min(460px,92vw); padding:38px 34px; border-radius:20px; text-align:center;
                  background:linear-gradient(180deg,rgba(23,29,41,.96),rgba(15,19,27,.96));
                  border:1px solid #2b3444; box-shadow:0 30px 80px rgba(0,0,0,.55); }
          .logo { width:72px; height:72px; margin:0 auto 18px; border-radius:20px; background:#16241c;
                  border:1px solid #2f5a3d; display:flex; align-items:center; justify-content:center;
                  font-size:30px; box-shadow:0 0 40px rgba(92,224,122,.25); }
          h1 { font-size:1.35rem; margin:0 0 6px; }
          p { color:#9aa7b8; font-size:.92rem; margin:0 0 18px; line-height:1.5; }
          .bar { height:4px; border-radius:3px; background:#1e2735; overflow:hidden; }
          .bar span { display:block; height:100%; width:38%; border-radius:3px;
                      background:linear-gradient(90deg,#5ce07a,#38bdf8); animation:slide 1.1s ease-in-out infinite; }
          @keyframes slide { 0% { margin-left:-38%; } 100% { margin-left:100%; } }
          .ok { color:#5ce07a; font-weight:600; }
          .err { color:#f87171; }
          a { color:#5ce07a; }
        </style>
        </head>
        <body>
          <div class="card">
            <div class="logo">🟢</div>
            <h1 id="title">Acceso con Google</h1>
            <p id="msg">Te redirigimos a Google para iniciar sesión en XD Launcher…</p>
            <div class="bar" id="bar"><span></span></div>
          </div>
        <script type="module">
          import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.5/firebase-app.js";
          import { getAuth, GoogleAuthProvider, signInWithRedirect, onAuthStateChanged } from "https://www.gstatic.com/firebasejs/10.12.5/firebase-auth.js";

          const cfg = {
            apiKey: "AIzaSyDegbqMJdTeNvkK17z4tRUW2490yV8FWXE",
            authDomain: "xd-launcher-8082d.firebaseapp.com",
            projectId: "xd-launcher-8082d",
            storageBucket: "xd-launcher-8082d.firebasestorage.app",
            messagingSenderId: "463172269791",
            appId: "1:463172269791:web:81ccbd0d79ba22a9b9db6a"
          };

          const title = document.getElementById('title');
          const msg = document.getElementById('msg');
          const bar = document.getElementById('bar');
          const START_KEY = 'xd_google_started';

          const post = (path, payload) => fetch(path, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
          });

          const fail = (error) => {
            title.textContent = 'No se pudo iniciar sesión';
            msg.innerHTML = '<span class="err">' + (error && error.message ? error.message : 'Error desconocido') + '</span>';
            bar.style.display = 'none';
            post('/error', { message: error && error.message ? error.message : 'Error de autenticación' });
          };

          const cameBack = new URLSearchParams(location.search).has('apiKey') ||
                           sessionStorage.getItem(START_KEY) === '1';

          try {
            const app = initializeApp(cfg);
            const auth = getAuth(app);

            if (cameBack) {
              // Volvemos de la redirección de Google: onAuthStateChanged es el evento
              // que restaura la sesión (getRedirectResult se queda esperando en
              // algunos navegadores, por eso no se usa).
              title.textContent = 'Recuperando tu sesión…';
              msg.textContent = 'Un momento, estamos confirmando el acceso con Google.';

              let restored = null;
              const unsubscribe = onAuthStateChanged(auth, (u) => {
                if (u) { restored = u; unsubscribe(); }
              });

              const deadline = Date.now() + 90000;
              while (Date.now() < deadline) {
                const user = restored || auth.currentUser;
                if (user) {
                  const idToken = await user.getIdToken();
                  await post('/token', { token: idToken, email: user.email || '' });
                  sessionStorage.removeItem(START_KEY);
                  title.textContent = 'Sesión iniciada';
                  msg.innerHTML = '<span class="ok">Sesión transferida al launcher.</span><br>Ya puedes cerrar esta pestaña.';
                  bar.style.display = 'none';
                  return;
                }
                await new Promise((r) => setTimeout(r, 400));
              }

              sessionStorage.removeItem(START_KEY);
              fail(new Error('No se completó el inicio de sesion con Google.'));
            } else {
              sessionStorage.setItem(START_KEY, '1');
              // signInWithRedirect navega a Google en la misma pestaña: nunca lo bloquea
              // el navegador ni los bloqueadores de ventanas emergentes.
              await signInWithRedirect(auth, new GoogleAuthProvider());
            }
          } catch (error) {
            fail(error);
          }
        </script>
        </body>
        </html>
        """;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts?.Cancel();
        try
        {
            _listener.Stop();
        }
        catch
        {
            // ignore
        }
    }
}
