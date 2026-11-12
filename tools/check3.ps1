$ErrorActionPreference='SilentlyContinue'
foreach($t in @('winget','choco','scoop')) { $c=Get-Command $t; if($c){Write-Output "$t -> $($c.Source)"} else {Write-Output "$t -> none"} }
Write-Output "--- arch ---"
$env:PROCESSOR_ARCHITECTURE
Write-Output "--- admin ---"
([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Output "--- disk ---"
Get-PSDrive C | Select-Object Used,Free
