Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class MouseDock
{
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
}
"@

$exe = "C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe"
$dir = "C:\Users\EVIL\Documents\xD\captures\dock"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 10

$sections = @(
    @{ Name = "01-inicio"; X = 541 },
    @{ Name = "02-java"; X = 637 },
    @{ Name = "03-juego"; X = 734 },
    @{ Name = "04-versiones"; X = 830 },
    @{ Name = "05-complementos"; X = 927 },
    @{ Name = "06-mods"; X = 1025 },
    @{ Name = "07-descargas"; X = 1121 },
    @{ Name = "08-ajustes"; X = 1218 },
    @{ Name = "09-movil"; X = 1313 }
)

foreach ($section in $sections) {
    [MouseDock]::Click($section.X, 914)
    Start-Sleep -Seconds 4
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bitmap.Save((Join-Path $dir ($section.Name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

if (-not $proc.HasExited) { $proc.Kill() }
Get-ChildItem $dir | Select-Object Name,Length
