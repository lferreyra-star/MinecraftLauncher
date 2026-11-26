Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ResizeClickCapture
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
"@

Start-Process -FilePath 'C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe' | Out-Null
Start-Sleep -Seconds 18

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw "XD Launcher is not running" }

[ResizeClickCapture]::SetWindowPos($proc.MainWindowHandle, [IntPtr]::Zero, 40, 40, 1120, 900, 0x0010 -bor 0x0002 -bor 0x0040) | Out-Null
Start-Sleep -Milliseconds 700

$lParam = [IntPtr]((86 -shl 16) -bor 913)
[ResizeClickCapture]::PostMessage($proc.MainWindowHandle, 0x0200, [IntPtr]0, $lParam) | Out-Null
[ResizeClickCapture]::PostMessage($proc.MainWindowHandle, 0x0201, [IntPtr]1, $lParam) | Out-Null
Start-Sleep -Milliseconds 80
[ResizeClickCapture]::PostMessage($proc.MainWindowHandle, 0x0202, [IntPtr]0, $lParam) | Out-Null
Start-Sleep -Milliseconds 1200

$rect = New-Object ResizeClickCapture+RECT
[ResizeClickCapture]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
[ResizeClickCapture]::PrintWindow($proc.MainWindowHandle, $hdc, 2) | Out-Null
$graphics.ReleaseHdc($hdc)
$bitmap.Save('C:\Users\EVIL\Documents\xD\captures\rebirth\settings-java-1120-final.png', [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
