Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class LoginVerifyUi
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
}
"@

$exe = 'C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Release\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe'
Start-Process -FilePath $exe | Out-Null
Start-Sleep -Seconds 16

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw 'XD Launcher no está corriendo' }

function Save-Shot([string]$name) {
    Start-Sleep -Milliseconds 700
    $rect = New-Object LoginVerifyUi+RECT
    [LoginVerifyUi]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { throw "Rect inválido: $w x $h" }
    $bitmap = New-Object System.Drawing.Bitmap($w, $h)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [LoginVerifyUi]::PrintWindow($proc.MainWindowHandle, $hdc, 2) | Out-Null
    $graphics.ReleaseHdc($hdc)
    $bitmap.Save("C:\Users\EVIL\Documents\xD\captures\$name", [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Output ("{0}: {1}x{2}" -f $name, $w, $h)
}

$flags = 0x0002 -bor 0x0040
[LoginVerifyUi]::SetWindowPos($proc.MainWindowHandle, [IntPtr]::Zero, 40, 40, 480, 700, $flags) | Out-Null
Save-Shot 'login-480x700.png'

[LoginVerifyUi]::SetWindowPos($proc.MainWindowHandle, [IntPtr]::Zero, 40, 40, 900, 600, $flags) | Out-Null
Save-Shot 'login-900x600.png'

[LoginVerifyUi]::ShowWindow($proc.MainWindowHandle, 3) | Out-Null
Save-Shot 'login-maximized.png'

Write-Output ("vivo=" + @(Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue).Count)
