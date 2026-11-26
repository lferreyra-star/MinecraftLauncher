$ErrorActionPreference = 'Stop'
$p = 'C:\Users\EVIL\AppData\Roaming\XDLauncher\settings.json'
$backup = 'C:\Users\EVIL\AppData\Local\Temp\opencode\settings-backup.json'
Copy-Item $p $backup -Force

foreach ($mode in @('Desactivado', 'Sutil')) {
    Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800
    $j = Get-Content $p -Raw | ConvertFrom-Json
    $j.motionLevel = $mode
    $j | ConvertTo-Json -Depth 12 | Set-Content $p -Encoding UTF8
    Start-Process -FilePath 'C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe' | Out-Null
    Start-Sleep -Seconds 18
    & 'C:\Users\EVIL\Documents\xD\tools\capture-transition.ps1' -X 985 -Y 155 -DelayMs 220 -Out ("C:\Users\EVIL\Documents\xD\captures\rebirth\mode-{0}.png" -f $mode)
}

Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Copy-Item $backup $p -Force
