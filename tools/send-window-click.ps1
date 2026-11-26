param([int]$X, [int]$Y)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class WindowClick
{
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
"@

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw "XD Launcher is not running" }

$lParam = [IntPtr](($Y -shl 16) -bor $X)
[WindowClick]::PostMessage($proc.MainWindowHandle, 0x0201, [IntPtr]1, $lParam) | Out-Null
Start-Sleep -Milliseconds 80
[WindowClick]::PostMessage($proc.MainWindowHandle, 0x0202, [IntPtr]0, $lParam) | Out-Null
