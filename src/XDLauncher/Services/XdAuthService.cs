using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using XdLauncher.Models;

namespace XdLauncher.Services;

/// <summary>Error de autenticación contra el sistema de cuentas de XD Launcher.</summary>
public sealed class XdAuthException : Exception
{
    public string? Code { get; init; }

    public XdAuthException(string message, string? code = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }
}

/// <summary>Cuenta de XD Launcher obtenida del servidor (Firebase Auth + Firestore).</summary>
public sealed class XdAccount
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? PhotoUrl { get; init; }
    public string Provider { get; set; } = "password";
    public required string IdToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddHours(1);
    public string? AvatarPath { get; set; }

    public string Initial => string.IsNullOrWhiteSpace(DisplayName)
        ? (string.IsNullOrWhiteSpace(Email) ? "?" : Email[..1].ToUpperInvariant())
        : DisplayName[..1].ToUpperInvariant();

    public string ProviderLabel => Provider switch
    {
        "google.com" => "Google",
        "password" => "Correo y contraseña",
        _ => Provider
    };
}

/// <summary>Perfil persistido en Firestore (colección users).</summary>
public sealed class XdUserProfile
{
    public required string UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string Provider { get; set; } = "password";
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? LastLoginAt { get; init; }
}

/// <summary>
/// Autenticación real contra el sistema de cuentas de XD Launcher (xdluncher.gt.tc).
/// Usa Firebase Authentication (Identity Toolkit) y Firestore, el mismo proyecto
/// y la misma colección "users" que la web. No hay usuarios locales ni contraseñas
/// almacenadas: la contraseña solo viaja por HTTPS al servidor de autenticación.
/// </summary>
public sealed class XdAuthService
{
    // Configuración pública del cliente web (mismo proyecto que xdluncher.gt.tc).
    private const string ApiKey = "AIzaSyDegbqMJdTeNvkK17z4tRUW2490yV8FWXE";
    private const string ProjectId = "xd-launcher-8082d";
    private const string IdentityBase = "https://identitytoolkit.googleapis.com/v1/accounts";
    private const string SecureTokenBase = "https://securetoken.googleapis.com/v1/token";
    private const string FirestoreBase = "https://firestore.googleapis.com/v1/projects/xd-launcher-8082d/databases/(default)/documents";

    /// <summary>Página de la web que traspasa la sesión de Google al launcher de escritorio.</summary>
    public const string DesktopHandoffUrl = "https://xdluncher.gt.tc/?desktop=1";

    /// <summary>Consola de Firebase donde se habilitan los proveedores de acceso.</summary>
    public const string FirebaseConsoleUrl =
        "https://console.firebase.google.com/project/xd-launcher-8082d/authentication/providers";

    private readonly HttpClient _http;

    public XdAuthService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("XDLauncher/1.3 (Windows 10; Win64; x64)");
    }

    /// <summary>
    /// Comprueba si el servidor de autenticación responde. Una respuesta del servicio
    /// (incluido un error de validación) confirma conectividad; solo un fallo de red
    /// se considera "sin conexión".
    /// </summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync($"{IdentityBase}:lookup?key={ApiKey}", content, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            LogService.Warn("El servidor de XD Launcher no respondió: " + ex.Message);
            return false;
        }
    }

    /// <summary>Inicia sesión con correo y contraseña contra Firebase Authentication.</summary>
    public async Task<XdAccount> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        LogService.Info("Intento de inicio de sesión de XD Launcher: " + email.Trim());
        var body = new Dictionary<string, object>
        {
            ["email"] = email.Trim(),
            ["password"] = password,
            ["returnSecureToken"] = true
        };

        var json = await PostAsync($"{IdentityBase}:signInWithPassword?key={ApiKey}", body, cancellationToken);
        var account = await BuildAccountAsync(json, cancellationToken);
        LogService.Info($"Sesión iniciada en XD Launcher: {account.Email}");
        return account;
    }

    /// <summary>Crea una cuenta nueva en el mismo sistema de usuarios de la web.</summary>
    public async Task<XdAccount> RegisterAsync(string email, string password, string displayName,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            ["email"] = email.Trim(),
            ["password"] = password,
            ["returnSecureToken"] = true
        };

        var json = await PostAsync($"{IdentityBase}:signUp?key={ApiKey}", body, cancellationToken);
        var account = await BuildAccountAsync(json, cancellationToken);

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var updateBody = new Dictionary<string, object>
            {
                ["idToken"] = account.IdToken,
                ["displayName"] = displayName.Trim(),
                ["returnSecureToken"] = true
            };
            try
            {
                var updated = await PostAsync($"{IdentityBase}:update?key={ApiKey}", updateBody, cancellationToken);
                account = await BuildAccountAsync(updated, cancellationToken);
            }
            catch (Exception ex)
            {
                LogService.Warn("No se pudo actualizar el nombre de la cuenta: " + ex.Message);
            }
        }

        return account;
    }

    /// <summary>
    /// Acepta un token de sesión de Firebase ya emitido (por ejemplo, el que entrega la web
    /// tras iniciar sesión con Google) y devuelve la cuenta validada contra el servidor.
    /// </summary>
    public async Task<XdAccount> SignInWithIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var account = await LookupAsync(idToken, cancellationToken);
        LogService.Info($"Sesión de XD Launcher validada por token: {account.Email}");
        return account;
    }

    /// <summary>
    /// Inicia sesión con Google sin salir de la aplicación: levanta un puente local que
    /// pide el consentimiento en el navegador del sistema y valida el idToken devuelto
    /// contra el servidor de autenticación.
    /// </summary>
    public async Task<XdAccount> SignInWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        using var bridge = new LocalAuthBridge();
        LogService.Info("Abriendo el acceso con Google en el navegador del sistema.");

        var idToken = await bridge.RequestGoogleTokenAsync(TimeSpan.FromMinutes(5), cancellationToken);
        var account = await SignInWithIdTokenAsync(idToken, cancellationToken);
        account.Provider = "google.com";
        return account;
    }

    /// <summary>Envía el correo real de recuperación de contraseña de Firebase.</summary>
    public async Task SendPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            ["requestType"] = "PASSWORD_RESET",
            ["email"] = email.Trim()
        };

        await PostAsync($"{IdentityBase}:sendOobCode?key={ApiKey}", body, cancellationToken);
    }

    /// <summary>Renueva el token de sesión usando el refresh token guardado.</summary>
    public async Task<XdAccount> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        using var response = await _http.PostAsync($"{SecureTokenBase}?key={ApiKey}", content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw BuildError(response, json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var idToken = GetString(root, "id_token") ?? throw new XdAuthException("El servidor no devolvió un token de sesión.");
        var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds) ? seconds : 3600;
        var newRefresh = GetString(root, "refresh_token") ?? refreshToken;

        var user = await LookupAsync(idToken, cancellationToken);
        return new XdAccount
        {
            UserId = user.UserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            PhotoUrl = user.PhotoUrl,
            Provider = user.Provider,
            IdToken = idToken,
            RefreshToken = newRefresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
        };
    }

    /// <summary>Consulta los datos reales del usuario en el servidor.</summary>
    public async Task<XdAccount> LookupAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["idToken"] = idToken };
        var json = await PostAsync($"{IdentityBase}:lookup?key={ApiKey}", body, cancellationToken);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("users", out var users) || users.ValueKind != JsonValueKind.Array)
            throw new XdAuthException("El servidor no devolvió los datos de la cuenta.");

        var user = users.EnumerateArray().FirstOrDefault();
        if (user.ValueKind == JsonValueKind.Undefined)
            throw new XdAuthException("La cuenta ya no existe en el servidor.", "user-not-found");

        return new XdAccount
        {
            UserId = GetString(user, "localId") ?? string.Empty,
            Email = GetString(user, "email") ?? string.Empty,
            DisplayName = GetString(user, "displayName"),
            PhotoUrl = GetString(user, "photoUrl"),
            Provider = GetString(user, "providerId") ?? "password",
            IdToken = idToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(55)
        };
    }

    /// <summary>Lee el documento users/{uid} de Firestore (perfil real del usuario).</summary>
    public async Task<XdUserProfile?> FetchProfileAsync(XdAccount account, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{FirestoreBase}/users/{account.UserId}");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + account.IdToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("fields", out var fields))
                return null;

            return new XdUserProfile
            {
                UserId = account.UserId,
                Name = ReadString(fields, "name") ?? account.DisplayName ?? string.Empty,
                Email = ReadString(fields, "email") ?? account.Email,
                PhotoUrl = ReadString(fields, "photoURL") ?? account.PhotoUrl,
                Provider = ReadString(fields, "provider") ?? account.Provider,
                CreatedAt = ReadTimestamp(fields, "createdAt"),
                LastLoginAt = ReadTimestamp(fields, "lastLoginAt")
            };
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo leer el perfil de Firestore: " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Sincroniza el documento del usuario con los datos que devuelve el servidor de auth.
    /// Usa el endpoint commit de Firestore con transformaciones de servidor (REQUEST_TIME),
    /// de modo que las reglas que exigen createdAt/lastLoginAt == request.time se cumplen
    /// exactamente igual que en la web. Si las reglas lo rechazan, el acceso se mantiene:
    /// el perfil de Authentication ya es válido.
    /// </summary>
    public async Task<bool> SyncProfileAsync(XdAccount account, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await ProfileExistsAsync(account, cancellationToken);
            var fields = new Dictionary<string, object>
            {
                ["uid"] = new Dictionary<string, object> { ["stringValue"] = account.UserId },
                ["name"] = new Dictionary<string, object> { ["stringValue"] = account.DisplayName ?? account.Email },
                ["email"] = new Dictionary<string, object> { ["stringValue"] = account.Email },
                ["photoURL"] = new Dictionary<string, object> { ["stringValue"] = account.PhotoUrl ?? string.Empty },
                ["provider"] = new Dictionary<string, object> { ["stringValue"] = account.Provider }
            };

            var fieldPaths = new List<object> { "uid", "name", "email", "photoURL", "provider" };
            var transforms = new Dictionary<string, object>
            {
                ["lastLoginAt"] = new Dictionary<string, object> { ["setToServerValue"] = "REQUEST_TIME" }
            };

            if (!exists)
            {
                fields["createdAt"] = new Dictionary<string, object>();
                fieldPaths.Add("createdAt");
                transforms["createdAt"] = new Dictionary<string, object> { ["setToServerValue"] = "REQUEST_TIME" };
            }

            var write = new Dictionary<string, object>
            {
                ["update"] = new Dictionary<string, object>
                {
                    ["name"] = $"projects/{ProjectId}/databases/(default)/documents/users/{account.UserId}",
                    ["fields"] = fields
                },
                ["updateMask"] = new Dictionary<string, object> { ["fieldPaths"] = fieldPaths },
                ["updateTransforms"] = transforms
            };

            var payload = new Dictionary<string, object> { ["writes"] = new[] { write } };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents:commit");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + account.IdToken);
            request.Content = content;

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                LogService.Warn($"Firestore rechazó la sincronización del perfil ({response.StatusCode}): {body}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo sincronizar el perfil en Firestore: " + ex.Message);
            return false;
        }
    }

    private async Task<bool> ProfileExistsAsync(XdAccount account, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{FirestoreBase}/users/{account.UserId}");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + account.IdToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Descarga la foto del perfil del servidor a la caché local para poder mostrarla
    /// sin bloquear la interfaz ni depender de la red en cada repintado.
    /// </summary>
    public async Task<string?> DownloadAvatarAsync(XdAccount account, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(account.PhotoUrl))
            return null;

        try
        {
            var target = Paths.GetAvatarCachePath("xd:" + account.UserId);
            if (!File.Exists(target))
            {
                var download = new Uri(account.PhotoUrl, UriKind.Absolute);
                var extension = Path.GetExtension(download.AbsolutePath);
                if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5)
                    extension = ".png";

                var destination = Path.ChangeExtension(target, extension);
                await DownloadService.DownloadAsync(account.PhotoUrl, destination, null, cancellationToken);
                target = destination;
            }

            account.AvatarPath = target;
            return target;
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo descargar el avatar de XD Launcher: " + ex.Message);
            return null;
        }
    }

    private async Task<XdAccount> BuildAccountAsync(string json, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var idToken = GetString(root, "idToken") ?? throw new XdAuthException("El servidor no devolvió un token de sesión.");
        var refreshToken = GetString(root, "refreshToken");
        var expiresIn = root.TryGetProperty("expiresIn", out var exp) && exp.TryGetInt64(out var seconds) ? seconds : 3600;
        var localId = GetString(root, "localId");
        var email = GetString(root, "email");
        var displayName = GetString(root, "displayName");
        var photoUrl = GetString(root, "photoUrl");

        if (string.IsNullOrWhiteSpace(localId) || string.IsNullOrWhiteSpace(email))
        {
            var lookup = await LookupAsync(idToken, cancellationToken);
            localId = lookup.UserId;
            email = lookup.Email;
            displayName ??= lookup.DisplayName;
            photoUrl ??= lookup.PhotoUrl;
        }

        return new XdAccount
        {
            UserId = localId ?? string.Empty,
            Email = email ?? string.Empty,
            DisplayName = displayName,
            PhotoUrl = photoUrl,
            Provider = "password",
            IdToken = idToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
        };
    }

    private async Task<string> PostAsync(string url, Dictionary<string, object> body, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw BuildError(response, text);

        return text;
    }

    private static XdAuthException BuildError(HttpResponseMessage response, string json)
    {
        var code = string.Empty;
        var message = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                code = GetString(error, "message") ?? string.Empty;
                message = GetString(error, "message") ?? string.Empty;
            }
        }
        catch
        {
            // Respuesta no JSON: se usa el código HTTP.
        }

        var friendly = code switch
        {
            "EMAIL_NOT_FOUND" => "No existe ninguna cuenta de XD Launcher con ese correo.",
            "INVALID_PASSWORD" => "La contraseña es incorrecta.",
            "INVALID_LOGIN_CREDENTIALS" => "El correo o la contraseña no son correctos.",
            "INVALID_EMAIL" => "El correo no tiene un formato válido.",
            "EMAIL_EXISTS" => "Ese correo ya está registrado en XD Launcher.",
            "USER_DISABLED" => "Esta cuenta está deshabilitada. Contacta con el soporte de XD Launcher.",
            "TOO_MANY_ATTEMPTS_TRY_LATER" => "Demasiados intentos. Espera unos minutos y vuelve a intentarlo.",
            "WEAK_PASSWORD" => "La contraseña es demasiado débil. Usa al menos 6 caracteres.",
            "PASSWORD_LOGIN_DISABLED" => "El acceso con correo y contraseña está deshabilitado en Firebase. Actívalo en Firebase Console > Authentication > Sign-in method > Email/Password. Mientras tanto, usa «Continuar con Google».",
            "OPERATION_NOT_ALLOWED" => "El proveedor de correo y contraseña no está habilitado en el servidor de XD Launcher. Actívalo en Firebase Console > Authentication > Sign-in method > Email/Password o usa «Continuar con Google».",
            "TOKEN_EXPIRED" => "La sesión caducó. Vuelve a iniciar sesión.",
            "INVALID_ID_TOKEN" => "La sesión ya no es válida. Vuelve a iniciar sesión.",
            "USER_NOT_FOUND" => "La cuenta ya no existe en el servidor.",
            _ => string.IsNullOrWhiteSpace(message)
                ? $"El servidor de XD Launcher respondió con el código {(int)response.StatusCode}."
                : message
        };

        return new XdAuthException(friendly, code);
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadString(JsonElement fields, string name)
    {
        if (!fields.TryGetProperty(name, out var value))
            return null;

        return value.TryGetProperty("stringValue", out var text) ? text.GetString() : null;
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement fields, string name)
    {
        if (!fields.TryGetProperty(name, out var value))
            return null;

        if (!value.TryGetProperty("timestampValue", out var text))
            return null;

        return DateTimeOffset.TryParse(text.GetString(), out var parsed) ? parsed : null;
    }
}
