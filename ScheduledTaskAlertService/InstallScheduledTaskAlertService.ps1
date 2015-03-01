$fullpath = (Get-Item -Path ".\" -Verbose).FullName
$cred = Get-Credential -Message "This service requires domain privledges."
New-Service -Name "ScheduledTaskAlertService" -DisplayName "ScheduledTaskAlertService" -Credential $cred -StartupType Automatic -BinaryPathName $fullpath\ScheduledTaskAlertService.exe -Description "Checks the last-run status of scheduled tasks and sends an email if the results don't match the expected value."

