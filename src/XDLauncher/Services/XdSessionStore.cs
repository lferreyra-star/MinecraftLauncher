using System.Text.Json;
using System.Text.Json.Serialization;

namespace XdLauncher.Services;

/// <summary>Sesión de XD Launcher guardada en disco.</summary>
public sealed class XdStoredSession
{
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    [JsonPropertyName("photoUrl")] public string? PhotoUrl { get; set; }
    [JsonPropertyName("provider")] public string? Provider { get; set; }
    [JsonPropertyName("protectedIdToken")] public string? ProtectedIdToken { get; set; }
    [JsonPropertyName("protectedRefreshToken")] public string? ProtectedRefreshToken { get; set; }
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; set; }
    [JsonPropertyName("savedAt")] public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// Guarda la sesión de XD Launcher cifrada con DPAPI (ámbito del usuario actual).
/// Los tokens nunca se escriben en texto plano y la contraseña nunca se almacena.
/// </summary>
public static class XdSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SessionFile => Path.Combine(Paths.AppDataRoot, "xd-session.json");

    public static void Save(XdAccount account)
    {
        try
        {
            var stored = new XdStoredSession
            {
                UserId = account.UserId,
                Email = account.Email,
                DisplayName = account.DisplayName,
                PhotoUrl = account.PhotoUrl,
                Provider = account.Provider,
                ProtectedIdToken = Protect(account.IdToken),
                ProtectedRefreshToken = string.IsNullOrWhiteSpace(account.RefreshToken)
                    ? null
                    : Protect(account.RefreshToken),
                ExpiresAt = account.ExpiresAt,
                SavedAt = DateTimeOffset.Now
            };

            Directory.CreateDirectory(Paths.AppDataRoot);
            var temp = SessionFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(stored, JsonOptions));
            File.Move(temp, SessionFile, true);
            LogService.Info($"Sesión de XD Launcher guardada para {stored.Email}.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo guardar la sesión de XD Launcher.");
        }
    }

    public static XdStoredSession? Load()
    {
        try
        {
            if (!File.Exists(SessionFile))
                return null;

            var stored = JsonSerializer.Deserialize<XdStoredSession>(File.ReadAllText(SessionFile), JsonOptions);
            if (stored is null || string.IsNullOrWhiteSpace(stored.ProtectedIdToken))
                return null;

            return stored;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo leer la sesión de XD Launcher.");
            return null;
        }
    }

    public static string? UnprotectToken(string? protectedToken)
    {
        if (string.IsNullOrWhiteSpace(protectedToken))
            return null;

        try
        {
            return AccountStore.Unprotect(Convert.FromBase64String(protectedToken));
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo descifrar el token guardado: " + ex.Message);
            return null;
        }
    }

    public static string Protect(string token)
        => Convert.ToBase64String(AccountStore.Protect(token));

    public static void Delete()
    {
        try
        {
            if (File.Exists(SessionFile))
                File.Delete(SessionFile);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo borrar la sesión de XD Launcher.");
        }
    }
}
