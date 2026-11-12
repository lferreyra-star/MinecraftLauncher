namespace XdLauncher.Models;

/// <summary>A physical Android device reported by adb.</summary>
public sealed class AndroidDeviceInfo
{
    public required string Serial { get; init; }
    public string State { get; set; } = "device";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? AndroidVersion { get; set; }
    public int SdkLevel { get; set; }
    public string? Resolution { get; set; }
    public string? WifiAddress { get; set; }
    public string? Product { get; set; }
    public bool IsSelected { get; set; }

    public bool IsAuthorized => State.Equals("device", StringComparison.OrdinalIgnoreCase);
    public bool IsUnauthorized => State.Equals("unauthorized", StringComparison.OrdinalIgnoreCase);
    public bool IsOffline => State.Equals("offline", StringComparison.OrdinalIgnoreCase);
    public bool IsWireless => Serial.Contains(':') || Serial.Contains("_adb-tls", StringComparison.OrdinalIgnoreCase);

    public string DisplayName
    {
        get
        {
            var name = string.Join(' ', new[] { Manufacturer, Model }
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim()));
            return string.IsNullOrWhiteSpace(name) ? Serial : name;
        }
    }

    public string ConnectionLabel => IsWireless ? "Wi-Fi" : "USB";
    public string AndroidLabel => string.IsNullOrWhiteSpace(AndroidVersion)
        ? "Android"
        : $"Android {AndroidVersion}{(SdkLevel > 0 ? $" · API {SdkLevel}" : string.Empty)}";

    public string StateLabel => State.ToLowerInvariant() switch
    {
        "device" => "Autorizado",
        "unauthorized" => "Permiso pendiente en el teléfono",
        "offline" => "Sin respuesta",
        _ => State
    };
}

public sealed class MdnsServiceInfo
{
    public required string Name { get; init; }
    public required string Address { get; init; }
    public string ServiceType { get; init; } = string.Empty;
    public bool IsPairing => ServiceType.Contains("pairing", StringComparison.OrdinalIgnoreCase);
    public bool IsConnect => ServiceType.Contains("connect", StringComparison.OrdinalIgnoreCase);
}

public sealed class ScrcpyReleaseInfo
{
    public required string Version { get; init; }
    public required string DownloadUrl { get; init; }
    public required string FileName { get; init; }
    public long Size { get; init; }
    public string? ChecksumsUrl { get; init; }
}

public sealed class MobileQualityOption
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public int MaxSize { get; init; }
}

/// <summary>A key bindable PC action. TouchTargetId links the action to a configurable touch point.</summary>
public sealed class InputActionInfo
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public int DefaultVirtualKey { get; init; }
    public bool IsMovement { get; init; }
    public string? TouchTargetId { get; init; }
    public string Glyph { get; init; } = "\uE765";
}

/// <summary>A touch position on the phone screen, stored as 0..1 normalized coordinates.</summary>
public sealed class TouchTargetInfo
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public double DefaultX { get; init; }
    public double DefaultY { get; init; }
    public bool IsJoystick { get; init; }
    public string Glyph { get; init; } = "\uE7C9";

    public double X { get; set; }
    public double Y { get; set; }

    public bool IsConfigured => X > 0.001 && Y > 0.001;
    public string PositionLabel => IsConfigured ? $"{X * 100:0}% · {Y * 100:0}%" : "Sin configurar";
}
