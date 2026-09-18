$ErrorActionPreference = 'SilentlyContinue'
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Select-Object ProcessId, ParentProcessId, CommandLine |
    Format-List | Out-String | Write-Output
Write-Output '--- watchdog cmds ---'
Get-CimInstance Win32_Process -Filter "Name='cmd.exe'" |
    Where-Object { $_.CommandLine -like '*watchdog*' } |
    Select-Object ProcessId, CommandLine |
    Format-List | Out-String | Write-Output
