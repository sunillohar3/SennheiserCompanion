Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.IsGenericMethod } | Select-Object -First 1

function Await-WinRt($asyncOp, $resultType) {
    $m = $asTaskGeneric.MakeGenericMethod($resultType)
    $task = $m.Invoke($null, @($asyncOp))
    $task.Wait()
    return $task.Result
}

[Windows.Devices.Bluetooth.BluetoothDevice, Windows.Devices.Bluetooth, ContentType = WindowsRuntime] | Out-Null
$devOp = [Windows.Devices.Bluetooth.BluetoothDevice]::FromIdAsync("Bluetooth#Bluetooth70:cf:49:5f:bb:89-80:c3:ba:ad:15:7f")
$dev = Await-WinRt $devOp ([Windows.Devices.Bluetooth.BluetoothDevice])

Write-Host "Device Name: $($dev.Name)"
Write-Host "ConnectionStatus: $($dev.ConnectionStatus)"
Write-Host "BluetoothAddress: $($dev.BluetoothAddress.ToString('X12'))"
Write-Host "ClassOfDevice: $($dev.ClassOfDevice.MajorClass)"
