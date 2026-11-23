Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class MouseInput2
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
$out = "C:\Users\EVIL\Documents\xD\captures\modpacks.png"

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 9
[MouseInput2]::Click(370, 460)
Start-Sleep -Seconds 6
[MouseInput2]::Click(638, 348)
Start-Sleep -Seconds 6

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bitmap.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

if (-not $proc.HasExited) {
    $proc.Kill()
}

Write-Output $out
