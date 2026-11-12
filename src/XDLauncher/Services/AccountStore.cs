using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XdLauncher.Services;

public sealed class StoredAccount
{
    [JsonPropertyName("protectedRefreshToken")] public string? ProtectedRefreshToken { get; set; }
    [JsonPropertyName("gamertag")] public string? Gamertag { get; set; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; set; }
    [JsonPropertyName("gamerscore")] public string? Gamerscore { get; set; }
    [JsonPropertyName("xuid")] public string? Xuid { get; set; }
    [JsonPropertyName("savedAt")] public DateTimeOffset SavedAt { get; set; }
}

/// <summary>Handles refresh-token persistence with Windows DPAPI (current user scope).</summary>
public static class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Save(string? refreshToken, Models.PlayerProfile? profile)
    {
        try
        {
            var stored = new StoredAccount
            {
                Gamertag = profile?.Gamertag,
                AvatarUrl = profile?.AvatarUrl,
                Gamerscore = profile?.Gamerscore,
                Xuid = profile?.Xuid,
                SavedAt = DateTimeOffset.Now,
                ProtectedRefreshToken = string.IsNullOrWhiteSpace(refreshToken)
                    ? null
                    : Convert.ToBase64String(Protect(refreshToken))
            };

            Directory.CreateDirectory(Paths.AppDataRoot);
            var temp = Paths.AccountFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(stored, JsonOptions));
            File.Move(temp, Paths.AccountFile, true);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo guardar la cuenta cifrada.");
        }
    }

    public static (string? RefreshToken, StoredAccount? Stored) Load()
    {
        try
        {
            if (!File.Exists(Paths.AccountFile))
                return (null, null);

            var stored = JsonSerializer.Deserialize<StoredAccount>(File.ReadAllText(Paths.AccountFile), JsonOptions);
            if (stored?.ProtectedRefreshToken is null)
                return (null, stored);

            var tokenBytes = Convert.FromBase64String(stored.ProtectedRefreshToken);
            var token = Unprotect(tokenBytes);
            return (token, stored);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo cargar la cuenta cifrada.");
            return (null, null);
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(Paths.AccountFile))
                File.Delete(Paths.AccountFile);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo borrar la cuenta guardada.");
        }
    }

    public static byte[] Protect(string text)
        => ProtectBytes(System.Text.Encoding.UTF8.GetBytes(text));

    public static string Unprotect(byte[] data)
        => System.Text.Encoding.UTF8.GetString(UnprotectBytes(data));

    public static byte[] ProtectBytes(byte[] data)
    {
        var input = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
        try
        {
            Marshal.Copy(data, 0, input.pbData, data.Length);
            if (!CryptProtectData(ref input, "XD Launcher", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return result;
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                    LocalFree(output.pbData);
            }
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(input.pbData);
        }
    }

    public static byte[] UnprotectBytes(byte[] data)
    {
        var input = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
        try
        {
            Marshal.Copy(data, 0, input.pbData, data.Length);
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return result;
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                    LocalFree(output.pbData);
            }
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(input.pbData);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
