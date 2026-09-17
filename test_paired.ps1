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

$selector = [Windows.Devices.Bluetooth.BluetoothDevice]::GetDeviceSelectorFromPairingState($true)
$op = [Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($selector)
$results = Await-WinRt $op ([Windows.Devices.Enumeration.DeviceInformationCollection])

Write-Host "Total paired Bluetooth devices: $($results.Count)"
foreach ($d in $results) {
    if ($d.Name -like "*Momentum*" -or $d.Name -like "*Sennheiser*") {
        Write-Host "MATCH PAIRED: $($d.Name) | Id: $($d.Id)"
    }
}
