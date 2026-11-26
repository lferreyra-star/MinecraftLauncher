Start-Process -FilePath 'C:\Users\EVIL\Documents\xD\src\XDLauncher\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MinecraftLauncher.exe' | Out-Null
foreach ($seconds in 3,6,9,12,15,18,21,24) {
    Start-Sleep -Seconds 3
    $processes = @(Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue)
    Write-Output ("t={0}s count={1}" -f $seconds, $processes.Count)
}
