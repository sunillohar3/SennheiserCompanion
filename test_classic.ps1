Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.IsGenericMethod } | Select-Object -First 1

function Await-WinRt($asyncOp, $resultType) {
    $m = $asTaskGeneric.MakeGenericMethod($resultType)
    $task = $m.Invoke($null, @($asyncOp))
    $task.Wait()
    return $task.Result
}

[Windows.Devices.Bluetooth.BluetoothDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime] | Out-Null
[Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType = WindowsRuntime] | Out-Null

$selector = [Windows.Devices.Bluetooth.BluetoothDevice]::GetDeviceSelectorFromDeviceName("MOMENTUM 4")
Write-Host "Selector: $selector"
$op = [Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($selector)
$results = Await-WinRt $op ([Windows.Devices.Enumeration.DeviceInformationCollection])

Write-Host "Found devices with BluetoothDevice selector: $($results.Count)"
foreach ($d in $results) {
    Write-Host "Name: $($d.Name) | Id: $($d.Id)"
}
