$p = Get-AppxPackage -Name Microsoft.XboxIdentityProvider | Select-Object -First 1
$p | Get-Member -MemberType Properties | Select-Object Name | Format-Wide -Column 4
Write-Output "--- Windows.ApplicationModel.Package properties ---"
$t = [Windows.ApplicationModel.Package,Windows.ApplicationModel,ContentType=WindowsRuntime]
$t.GetProperties() | Select-Object Name,PropertyType | Format-Table -AutoSize
