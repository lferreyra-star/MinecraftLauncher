Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw 'No launcher' }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
foreach ($element in $all) {
    $type = $element.Current.ControlType
    if ($type -eq [System.Windows.Automation.ControlType]::Button -or
        $type -eq [System.Windows.Automation.ControlType]::ListItem) {
        Write-Output ("{0} | {1}" -f $type.ProgrammaticName, $element.Current.Name)
    }
}
