param([int]$Width = 1000, [int]$Height = 800)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ResizeWin
{
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw "XD Launcher is not running" }

[ResizeWin]::SetWindowPos($proc.MainWindowHandle, [IntPtr](-1), 240, 70, $Width, $Height, 0x0002 -bor 0x0040) | Out-Null
[ResizeWin]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 600
