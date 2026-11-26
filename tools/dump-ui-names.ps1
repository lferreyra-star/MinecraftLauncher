Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw 'No launcher' }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
foreach ($element in $all) {
    $name = $element.Current.Name
    if ($name -and ($name -match 'justes|Cuenta|Ajust')) {
        Write-Output ($element.Current.ControlType.ProgrammaticName + ' | ' + $name)
    }
}
