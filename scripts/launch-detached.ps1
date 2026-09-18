$ErrorActionPreference = 'Stop'
# Instala auto-arranque al iniciar sesion (carpeta Inicio, sin admin) y
# lanza el watchdog separado via WMI para que sobreviva al cierre de consolas.
# URL fija: http://localhost:5215 (sin tunel, nunca cambia).

$root = Split-Path -Parent $PSScriptRoot
$watchdog = Join-Path $root 'servidor-watchdog.bat'
if (-not (Test-Path -LiteralPath $watchdog)) { throw "No existe $watchdog" }
$dll = Join-Path $root 'bin\Release\net10.0\publish\INNOVATE INDUSTRIES WEB STORE.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "No existe $dll. Ejecuta 'dotnet publish -c Release' primero." }

$startup = Join-Path ([Environment]::GetFolderPath('Startup')) 'InnovateStore.lnk'
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($startup)
$sc.TargetPath = $watchdog
$sc.WorkingDirectory = $root
$sc.WindowStyle = 7
$sc.Description = 'INNOVATE STORE servidor local fijo http://localhost:5215'
$sc.Save()
Write-Output "Auto-arranque instalado: $startup"

$cmd = 'cmd /c ""' + $watchdog + '""'
$r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $cmd; CurrentDirectory = $root }
Write-Output "Watchdog lanzado (PID $($r.ProcessId))."
