$ErrorActionPreference = 'SilentlyContinue'
# Detiene supervisor + servidor publicado (no toca VS ni otros dotnet).
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object { $_.CommandLine -like '*INNOVATE INDUSTRIES WEB STORE.dll*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Get-CimInstance Win32_Process -Filter "Name='INNOVATE INDUSTRIES WEB STORE.exe'" |
    Where-Object { $_.CommandLine -like '*publish*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Get-CimInstance Win32_Process -Filter "Name='cmd.exe'" |
    Where-Object { $_.CommandLine -like '*servidor-watchdog.bat*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Write-Output 'Servidor detenido.'
