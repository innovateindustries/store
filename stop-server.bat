@echo off
REM INNOVATE STORE - detiene el servidor local (watchdog + dotnet del proyecto).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-all.ps1"
