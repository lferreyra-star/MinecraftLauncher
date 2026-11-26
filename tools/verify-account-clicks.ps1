Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class AccountUi
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
}
"@

$exe = 'C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Release\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe'
Start-Process -FilePath $exe | Out-Null
Start-Sleep -Seconds 18

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw 'XD Launcher no está corriendo' }

[AccountUi]::SetWindowPos($proc.MainWindowHandle, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040) | Out-Null
[AccountUi]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 400
[AccountUi]::Click(1160, 155)
Start-Sleep -Milliseconds 900
[AccountUi]::Click(610, 486)
Start-Sleep -Milliseconds 900

$rect = New-Object AccountUi+RECT
[AccountUi]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
[AccountUi]::PrintWindow($proc.MainWindowHandle, $hdc, 2) | Out-Null
$graphics.ReleaseHdc($hdc)
$bitmap.Save('C:\Users\EVIL\Documents\xD\captures\rebirth\settings-cuenta-fix.png', [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
$proc.Refresh()
Write-Output ("vivo=" + @(Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue).Count)
