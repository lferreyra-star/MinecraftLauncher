Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class VerifyAccountUi
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
"@

$settingsPath = 'C:\Users\EVIL\AppData\Roaming\XDLauncher\settings.json'
if (Test-Path $settingsPath) {
    $json = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
    $json | Add-Member -NotePropertyName 'onboardingCompleted' -NotePropertyValue $true -Force
    $json | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $settingsPath
}

$exe = 'C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Release\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe'
Start-Process -FilePath $exe | Out-Null
Start-Sleep -Seconds 20

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw 'XD Launcher no está corriendo' }

$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

function Invoke-Text([string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (-not $element) { throw "No se encontró: $name" }
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $parent = $walker.GetParent($element)
    while ($parent -and $parent.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button -and
           $parent.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem) {
        $parent = $walker.GetParent($parent)
    }
    if (-not $parent) { throw "Sin contenedor accionable para: $name" }
    $pattern = $parent.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

Invoke-Text 'Ajustes'
Start-Sleep -Milliseconds 1200
Invoke-Text 'Cuenta'
Start-Sleep -Milliseconds 1200

$rect = New-Object VerifyAccountUi+RECT
[VerifyAccountUi]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
[VerifyAccountUi]::PrintWindow($proc.MainWindowHandle, $hdc, 2) | Out-Null
$graphics.ReleaseHdc($hdc)
$bitmap.Save('C:\Users\EVIL\Documents\xD\captures\rebirth\settings-cuenta-fix.png', [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
$proc.Refresh()
Write-Output ("procesos_vivos=" + @(Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue).Count)
