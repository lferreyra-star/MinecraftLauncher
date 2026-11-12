using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using XdLauncher.Models;

namespace XdLauncher.Services;

public sealed class AuthException : Exception
{
    public bool ConfigurationRequired { get; init; }
    public string? ErrorCode { get; init; }

    public AuthException(string message, string? errorCode = null, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}

public sealed class DeviceCodeInfo
{
    public required string DeviceCode { get; init; }
    public required string UserCode { get; init; }
    public required string VerificationUri { get; init; }
    public string? VerificationUriComplete { get; init; }
    public required string Message { get; init; }
    public required int ExpiresIn { get; init; }
    public required int Interval { get; init; }
}

public sealed class SignInResult
{
    public required PlayerProfile Profile { get; init; }
    public required string RefreshToken { get; init; }
}

public sealed class MinecraftAuthInfo
{
    public required string AccessToken { get; init; }
    public required string Uuid { get; init; }
    public required string Name { get; init; }
    public string? RefreshToken { get; init; }
}

/// <summary>
/// Microsoft identity platform device-code flow + Xbox Live (XSTS) profile lookup.
/// Requires a Microsoft Entra public client configured for XboxLive.signin.
/// Passwords are never requested or stored. Refresh tokens are encrypted with DPAPI.
/// </summary>
public sealed class AuthService
{
    private const string DeviceCodeEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XboxUserAuthEndpoint = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsEndpoint = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string ProfileEndpoint = "https://profile.xboxlive.com/users/me/profile/settings?settings=Gamertag,GameDisplayPicRaw,Gamerscore,AccountTier";
    private const string Scope = "XboxLive.signin offline_access openid profile";
    private readonly HttpClient _http;

    public AuthService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("XDLauncher/1.0 (Windows 10; Win64; x64)");
    }

    public bool IsConfigured(string clientId) => !string.IsNullOrWhiteSpace(clientId);

    public async Task<DeviceCodeInfo> RequestDeviceCodeAsync(string clientId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured(clientId);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["scope"] = Scope
        });

        using var response = await _http.PostAsync(DeviceCodeEndpoint, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw CreateOAuthException("No se pudo iniciar el flujo de código de dispositivo.", json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new DeviceCodeInfo
        {
            DeviceCode = GetString(root, "device_code") ?? throw new AuthException("Respuesta de Microsoft inválida."),
            UserCode = GetString(root, "user_code") ?? throw new AuthException("Respuesta de Microsoft inválida."),
            VerificationUri = GetString(root, "verification_uri") ?? "https://microsoft.com/link",
            VerificationUriComplete = GetString(root, "verification_uri_complete"),
            Message = GetString(root, "message") ?? "Abre la página indicada e introduce el código.",
            ExpiresIn = root.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 900,
            Interval = root.TryGetProperty("interval", out var interval) ? interval.GetInt32() : 5
        };
    }

    public async Task<SignInResult> CompleteSignInAsync(string clientId, DeviceCodeInfo deviceCode,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured(clientId);
        var interval = Math.Max(2, deviceCode.Interval);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(deviceCode.ExpiresIn);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

            var result = await PollTokenAsync(clientId, deviceCode.DeviceCode, cancellationToken);
            if (result.Pending)
            {
                if (result.SlowDown)
                    interval += 5;
                continue;
            }

            if (result.Token is null)
                throw result.Error ?? new AuthException("Microsoft rechazó el inicio de sesión.");

            return await FinishSignInAsync(result.Token, cancellationToken);
        }

        throw new AuthException("El código de inicio de sesión caducó. Solicita uno nuevo.", "expired_token");
    }

    public async Task<SignInResult> RestoreAsync(string clientId, string refreshToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured(clientId);
        var token = await RefreshTokenAsync(clientId, refreshToken, cancellationToken);
        return await FinishSignInAsync(token, cancellationToken);
    }

    /// <summary>
    /// Obtiene un token oficial de Minecraft Services a partir del refresh token de Microsoft.
    /// Solo funciona con cuentas que poseen Minecraft Java Edition. No se falsifica ninguna identidad.
    /// </summary>
    public async Task<MinecraftAuthInfo> GetMinecraftAuthAsync(string clientId, string refreshToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured(clientId);
        var token = await RefreshTokenAsync(clientId, refreshToken, cancellationToken);
        var xbox = await AuthenticateXboxAsync(token.AccessToken, cancellationToken,
            "rp://api.minecraftservices.com/");

        var loginJson = await PostJsonAsync("https://api.minecraftservices.com/authentication/login_with_xbox",
            new { identityToken = $"XBL3.0 x={xbox.UserHash};{xbox.XstsToken}" }, cancellationToken);
        using var loginDoc = JsonDocument.Parse(loginJson);
        var access = GetString(loginDoc.RootElement, "access_token")
                     ?? throw new AuthException("Minecraft no devolvió un token de acceso.");

        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.minecraftservices.com/minecraft/profile");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + access);
        using var response = await _http.SendAsync(request, cancellationToken);
        var profileJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthException(
                response.StatusCode == HttpStatusCode.NotFound
                    ? "Esta cuenta de Microsoft no tiene Minecraft Java Edition. Usa un perfil local o inicia sesión con la cuenta que posee el juego."
                    : "Minecraft rechazó la sesión. Vuelve a iniciar sesión con Microsoft.",
                ((int)response.StatusCode).ToString());
        }

        using var profileDoc = JsonDocument.Parse(profileJson);
        var name = GetString(profileDoc.RootElement, "name")
                   ?? throw new AuthException("Minecraft no devolvió el nombre del jugador.");
        var id = GetString(profileDoc.RootElement, "id")
                 ?? throw new AuthException("Minecraft no devolvió el UUID del jugador.");

        return new MinecraftAuthInfo
        {
            AccessToken = access,
            Uuid = FormatUuid(id),
            Name = name,
            RefreshToken = token.RefreshToken
        };
    }

    private static string FormatUuid(string id)
    {
        var raw = id.Replace("-", string.Empty);
        if (raw.Length != 32)
            return id;
        return $"{raw[..8]}-{raw[8..12]}-{raw[12..16]}-{raw[16..20]}-{raw[20..]}";
    }

    private async Task<SignInResult> FinishSignInAsync(OAuthToken token, CancellationToken cancellationToken)    {
        var xbox = await AuthenticateXboxAsync(token.AccessToken, cancellationToken);
        var profile = await GetProfileAsync(xbox, cancellationToken);
        return new SignInResult
        {
            Profile = profile,
            RefreshToken = token.RefreshToken ?? throw new AuthException("Microsoft no devolvió un token de actualización.")
        };
    }

    private async Task<(bool Pending, bool SlowDown, OAuthToken? Token, AuthException? Error)> PollTokenAsync(
        string clientId, string deviceCode, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["device_code"] = deviceCode
        });

        using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (response.IsSuccessStatusCode)
            return (false, false, ParseToken(root), null);

        var error = GetString(root, "error") ?? "unknown_error";
        switch (error)
        {
            case "authorization_pending":
                return (true, false, null, null);
            case "slow_down":
                return (true, true, null, null);
            case "expired_token":
                return (false, false, null, new AuthException("El código caducó. Solicita uno nuevo.", error));
            case "authorization_declined":
                return (false, false, null, new AuthException("El inicio de sesión fue cancelado en la página de Microsoft.", error));
            case "bad_verification_code":
                return (false, false, null, new AuthException("Microsoft no reconoce este código.", error));
            default:
            {
                var description = GetString(root, "error_description") ?? error;
                return (false, false, null, new AuthException(
                    $"Microsoft devolvió un error: {description}", error));
            }
        }
    }

    private async Task<OAuthToken> RefreshTokenAsync(string clientId, string refreshToken, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = Scope
        });

        using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = ExtractOAuthError(json);
            throw new AuthException(
                error == "invalid_grant"
                    ? "La sesión guardada caducó. Vuelve a iniciar sesión con Microsoft."
                    : "No se pudo renovar la sesión de Microsoft.", error);
        }

        using var doc = JsonDocument.Parse(json);
        return ParseToken(doc.RootElement);
    }

    private static OAuthToken ParseToken(JsonElement root)
    {
        var access = GetString(root, "access_token") ?? throw new AuthException("Microsoft no devolvió un token de acceso.");
        var refresh = GetString(root, "refresh_token");
        var expires = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        return new OAuthToken(access, refresh, expires);
    }

    private async Task<XboxAuth> AuthenticateXboxAsync(string accessToken, CancellationToken cancellationToken,
        string relyingParty = "http://xboxlive.com")
    {
        var userPayload = new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = "d=" + accessToken
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        };

        var userJson = await PostJsonAsync(XboxUserAuthEndpoint, userPayload, cancellationToken);
        using var userDoc = JsonDocument.Parse(userJson);
        var userToken = GetString(userDoc.RootElement, "Token")
                        ?? throw new AuthException("Xbox Live no devolvió un token de usuario.");

        var xstsPayload = new
        {
            Properties = new
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { userToken }
            },
            RelyingParty = relyingParty,
            TokenType = "JWT"
        };

        using var xstsResponse = await PostJsonResponseAsync(XstsEndpoint, xstsPayload, cancellationToken);
        var xstsJson = await xstsResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!xstsResponse.IsSuccessStatusCode)
        {
            var xerr = ExtractXboxError(xstsJson);
            throw new AuthException(MapXboxError(xerr), xerr);
        }

        using var xstsDoc = JsonDocument.Parse(xstsJson);
        var xstsToken = GetString(xstsDoc.RootElement, "Token")
                        ?? throw new AuthException("Xbox Live no devolvió un token XSTS.");
        var userHash = string.Empty;
        if (xstsDoc.RootElement.TryGetProperty("DisplayClaims", out var claims) &&
            claims.TryGetProperty("xui", out var xui) &&
            xui.ValueKind == JsonValueKind.Array &&
            xui.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } first &&
            first.TryGetProperty("uhs", out var uhs))
        {
            userHash = uhs.GetString() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(userHash))
            throw new AuthException("Xbox Live devolvió una respuesta sin identificador de usuario.");

        return new XboxAuth(userHash, xstsToken);
    }

    private async Task<PlayerProfile> GetProfileAsync(XboxAuth auth, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProfileEndpoint);
        request.Headers.TryAddWithoutValidation("Authorization", $"XBL3.0 x={auth.UserHash};{auth.XstsToken}");
        request.Headers.TryAddWithoutValidation("x-xbl-contract-version", "2");
        request.Headers.TryAddWithoutValidation("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");

        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new AuthException("No se pudo leer el perfil de Xbox Live.", ((int)response.StatusCode).ToString());

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("profileUsers", out var users) ||
            users.ValueKind != JsonValueKind.Array ||
            users.EnumerateArray().FirstOrDefault() is not { ValueKind: JsonValueKind.Object } user)
        {
            throw new AuthException("Xbox Live devolvió un perfil vacío.");
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (user.TryGetProperty("settings", out var settingsArray) && settingsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var setting in settingsArray.EnumerateArray())
            {
                var id = GetString(setting, "id");
                var value = GetString(setting, "value");
                if (id is not null && value is not null)
                    settings[id] = value;
            }
        }

        var gamertag = settings.GetValueOrDefault("Gamertag") ?? "Jugador de Xbox";
        return new PlayerProfile
        {
            Gamertag = gamertag,
            AvatarUrl = settings.GetValueOrDefault("GameDisplayPicRaw"),
            Gamerscore = settings.GetValueOrDefault("Gamerscore"),
            Xuid = GetString(user, "id"),
            IsMicrosoftAccount = true
        };
    }

    private async Task<string> PostJsonAsync<T>(string url, T payload, CancellationToken cancellationToken)
    {
        using var response = await PostJsonResponseAsync(url, payload, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var xerr = ExtractXboxError(json);
            if (xerr is not null)
                throw new AuthException(MapXboxError(xerr), xerr);
            throw new AuthException($"El servicio de Xbox Live devolvió {(int)response.StatusCode}.");
        }
        return json;
    }

    private Task<HttpResponseMessage> PostJsonResponseAsync<T>(string url, T payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return _http.PostAsync(url, content, cancellationToken);
    }

    private static string? ExtractOAuthError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return GetString(doc.RootElement, "error");
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractXboxError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("XErr", out var xerr)
                ? xerr.GetRawText().Trim('"')
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string MapXboxError(string? xerr) => xerr switch
    {
        "2148916233" => "Esta cuenta de Microsoft no tiene un perfil de Xbox. Crea uno gratuitamente en xbox.com y vuelve a intentarlo.",
        "2148916235" => "Xbox Live no está disponible en la región de esta cuenta.",
        "2148916236" or "2148916237" => "Esta cuenta necesita verificación de edad en Xbox Live.",
        "2148916238" => "Esta cuenta es infantil y debe añadirse a un grupo familiar de Microsoft.",
        "2148916227" => "Xbox Live indica que esta cuenta está suspendida.",
        "2148916229" => "Xbox Live ha bloqueado temporalmente el acceso de esta cuenta.",
        _ => "Xbox Live rechazó el inicio de sesión. Comprueba tu cuenta en xbox.com."
    };

    private static void EnsureConfigured(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new AuthException(
                "Falta el Client ID de Microsoft Entra. Regístralo en el portal de Azure y pégalo en Ajustes → Cuenta.",
                "missing_client_id")
            {
                ConfigurationRequired = true
            };
        }
    }

    private static AuthException CreateOAuthException(string message, string json)
    {
        var description = string.Empty;
        var code = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            description = GetString(doc.RootElement, "error_description") ?? string.Empty;
            code = GetString(doc.RootElement, "error") ?? string.Empty;
        }
        catch
        {
            // ignore
        }

        if (code is "unauthorized_client" or "invalid_client")
        {
            return new AuthException(
                "Microsoft no reconoce el Client ID. Debe ser una aplicación pública (cliente nativo) registrada en Microsoft Entra.",
                code)
            {
                ConfigurationRequired = true
            };
        }

        return new AuthException(description.Length > 0 ? $"{message} {description}" : message, code);
    }

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record OAuthToken(string AccessToken, string? RefreshToken, int ExpiresInSeconds);
    private sealed record XboxAuth(string UserHash, string XstsToken);
}
