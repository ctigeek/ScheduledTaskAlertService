Stop-Service "ScheduledTaskAlertService"
Start-Sleep -Seconds 3

$service = Get-WmiObject -Class Win32_Service -Filter "Name='ScheduledTaskAlertService'"
$service.delete()
