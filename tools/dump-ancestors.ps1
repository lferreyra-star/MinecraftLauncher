Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process -Name MinecraftLauncher -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw 'No launcher' }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'Ajustes')
$element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
$walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
$current = $element
for ($i = 0; $i -lt 12 -and $current; $i++) {
    Write-Output ("{0} | {1}" -f $current.Current.ControlType.ProgrammaticName, $current.Current.Name)
    $current = $walker.GetParent($current)
}
