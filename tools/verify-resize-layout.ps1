param([string]$Out = 'C:\Users\EVIL\Documents\xD\captures\rebirth\java-settings-resized.png', [int]$Width = 1120, [int]$Height = 900)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ResizeCapture
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
"@

Start-Process -FilePath 'C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe' | Out-Null
Start-Sleep -Seconds 18

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw "XD Launcher is not running" }

[ResizeCapture]::SetWindowPos($proc.MainWindowHandle, [IntPtr]::Zero, 40, 40, $Width, $Height, 0x0010 -bor 0x0002 -bor 0x0040) | Out-Null
Start-Sleep -Milliseconds 800

$rect = New-Object ResizeCapture+RECT
[ResizeCapture]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
[ResizeCapture]::PrintWindow($proc.MainWindowHandle, $hdc, 2) | Out-Null
$graphics.ReleaseHdc($hdc)
$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
