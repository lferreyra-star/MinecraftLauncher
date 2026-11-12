$ErrorActionPreference = 'SilentlyContinue'
Write-Output "=== Minecraft packages ==="
Get-AppxPackage -Name Microsoft.MinecraftUWP,Microsoft.MinecraftWindowsBeta | Select-Object Name,Version,PackageFullName,InstallLocation,Status | Format-List
Write-Output "=== Xbox/Gaming dependencies ==="
Get-AppxPackage | Where-Object { $_.Name -match 'GamingServices|XboxIdentityProvider|XboxGamingOverlay|GamingApp|VCLibs' } | Select-Object Name,Version | Format-Table -AutoSize
Write-Output "=== Tools ==="
foreach ($t in @('iscc','makensis','wix','7z','git','node','npm')) { $c = Get-Command $t; if ($c) { Write-Output "$t -> $($c.Source)" } else { Write-Output "$t -> NOT FOUND" } }
Write-Output "=== .NET runtimes ==="
dotnet --list-runtimes
