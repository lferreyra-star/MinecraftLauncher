Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-30)} -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -match 'MinecraftLauncher|XD Launcher|Exception' } |
    Select-Object -First 10 TimeCreated, Id, LevelDisplayName, Message |
    Format-List
