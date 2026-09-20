# Instala la sincronizacion automatica Admin local -> GitHub Pages (cada 5 min).
# Uso (una vez): powershell -ExecutionPolicy Bypass -File scripts/install-sync-task.ps1
# Ver: Get-ScheduledTask -TaskName InnovateSyncPages | Desinstalar: Unregister-ScheduledTask -TaskName InnovateSyncPages -Confirm:$false
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ps = Join-Path $root 'scripts/sync-pages.ps1'
$task = 'InnovateSyncPages'

Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ("-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"" + $ps + "`"") -WorkingDirectory $root
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 5)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName $task -Action $action -Trigger $trigger -Settings $settings -Description 'Sincroniza Admin local con GitHub Pages' | Out-Null
Write-Output 'Tarea instalada: cada 5 min. Log en %TEMP%\innovate-sync.log'
