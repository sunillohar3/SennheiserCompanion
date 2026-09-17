Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.IsGenericMethod } | Select-Object -First 1

function Await-WinRt($asyncOp, $resultType) {
    $m = $asTaskGeneric.MakeGenericMethod($resultType)
    $task = $m.Invoke($null, @($asyncOp))
    $task.Wait()
    return $task.Result
}

[Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType = WindowsRuntime] | Out-Null
$props = @("{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2", "System.Devices.BatteryLifePercent")
$op = [Windows.Devices.Enumeration.DeviceInformation]::CreateFromIdAsync("Bluetooth#Bluetooth70:cf:49:5f:bb:89-80:c3:ba:ad:15:7f", $props)
$devInfo = Await-WinRt $op ([Windows.Devices.Enumeration.DeviceInformation])

Write-Host "Device: $($devInfo.Name)"
foreach ($k in $devInfo.Properties.Keys) {
    Write-Host "Prop $k = $($devInfo.Properties[$k])"
}
