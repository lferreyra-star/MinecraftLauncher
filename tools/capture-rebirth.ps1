Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class RebirthUi
{
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
}
"@

$dir = "C:\Users\EVIL\Documents\xD\captures\rebirth\sections"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$exe = "C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe"

function Ensure-Launcher {
    $proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $proc) {
        Start-Process -FilePath $exe | Out-Null
        Start-Sleep -Seconds 14
        $proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    }
    if ($proc) {
        [RebirthUi]::ShowWindow($proc.MainWindowHandle, 9) | Out-Null
        [RebirthUi]::SetWindowPos($proc.MainWindowHandle, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040) | Out-Null
        [RebirthUi]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
        [RebirthUi]::SwitchToThisWindow($proc.MainWindowHandle, $true)
        try { [Microsoft.VisualBasic.Interaction]::AppActivate($proc.Id) } catch {}
    }
    Start-Sleep -Milliseconds 900
}

$sections = @(
    @{ Name = "01-inicio"; X = 350; Y = 216 },
    @{ Name = "02-java"; X = 350; Y = 265 },
    @{ Name = "03-juego"; X = 350; Y = 314 },
    @{ Name = "04-movil"; X = 350; Y = 370 },
    @{ Name = "05-versiones"; X = 350; Y = 446 },
    @{ Name = "06-complementos"; X = 350; Y = 496 },
    @{ Name = "07-mods"; X = 350; Y = 546 },
    @{ Name = "08-descargas"; X = 350; Y = 600 },
    @{ Name = "09-perfil"; X = 350; Y = 678 },
    @{ Name = "10-ajustes"; X = 350; Y = 728 }
)

foreach ($section in $sections) {
    Ensure-Launcher
    [RebirthUi]::Click($section.X, $section.Y)
    Start-Sleep -Seconds 3
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bitmap.Save((Join-Path $dir ($section.Name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

Get-ChildItem $dir | Select-Object Name, Length
