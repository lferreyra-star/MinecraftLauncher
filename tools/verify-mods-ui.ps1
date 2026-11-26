Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ModsVerifyUi
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

$root = 'C:\Users\EVIL\AppData\Roaming\XDLauncher'
$session = Join-Path $root 'xd-session.json'
$invalid = Join-Path $root 'xd-session.json.invalid'
$backup = Join-Path $root 'xd-session.json.verify-backup'
if (Test-Path $session) { Copy-Item $session $backup -Force }
if (Test-Path $invalid) { Copy-Item $invalid $session -Force }

$exe = 'C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Release\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe'
Start-Process -FilePath $exe | Out-Null
Start-Sleep -Seconds 20

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw 'XD Launcher no está corriendo' }

[ModsVerifyUi]::SetWindowPos($proc.MainWindowHandle, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040) | Out-Null
[ModsVerifyUi]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 500
[ModsVerifyUi]::Click(1000, 155)
Start-Sleep -Seconds 5

function Save-Shot([string]$name) {
    $rect = New-Object ModsVerifyUi+RECT
    [ModsVerifyUi]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [ModsVerifyUi]::PrintWindow($proc.MainWindowHandle, $hdc, 2) | Out-Null
    $graphics.ReleaseHdc($hdc)
    $bitmap.Save("C:\Users\EVIL\Documents\xD\captures\rebirth\$name", [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

Save-Shot 'mods-version-catalog.png'
[ModsVerifyUi]::Click(700, 300)
Start-Sleep -Seconds 3
Save-Shot 'mods-version-details.png'
Write-Output ("vivo=" + @(Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue).Count)
