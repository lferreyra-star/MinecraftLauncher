$root = 'C:\Users\EVIL\AppData\Roaming\XDLauncher'
$session = Join-Path $root 'xd-session.json'
$backup = Join-Path $root 'xd-session.json.verify-backup'
Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $backup) {
    Move-Item $backup $session -Force
}
elseif (Test-Path $session) {
    Remove-Item $session -Force
}
