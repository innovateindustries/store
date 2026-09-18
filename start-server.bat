@echo off
REM INNOVATE STORE - doble clic: levanta servidor + tunel y abre la pagina.
REM Si el tunel sigue vivo, REUTILIZA el mismo link. No cierres esta ventana.
cd /d "%~dp0"
curl -s -o nul http://localhost:5215/api/launcher/ping
if errorlevel 1 (
  echo Arrancando servidor local...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\launch-detached.ps1"
  if errorlevel 1 pause & exit /b 1
  echo Esperando arranque...
  timeout /t 12 /nobreak >nul
  curl -s http://localhost:5215/api/launcher/ping
  echo.
) else (
  echo El servidor ya esta corriendo en http://localhost:5215
)
echo.
echo Comprobando URL publica...
set PUBURL=
if exist "%TEMP%\innovate-public-url.txt" set /p PUBURL=<"%TEMP%\innovate-public-url.txt"
set PUBOK=0
if defined PUBURL (
  curl -s -o nul --max-time 10 "%PUBURL%/api/launcher/ping"
  if not errorlevel 1 set PUBOK=1
)
if %PUBOK%==1 (
  echo URL publica activa, mismo link: %PUBURL%
) else (
  echo El tunel no responde. Lanzando tunel...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\public-tunnel.ps1"
  set /p PUBURL=<"%TEMP%\innovate-public-url.txt"
)
echo.
echo Abriendo la pagina...
if defined PUBURL (
  start "" "%PUBURL%"
) else (
  start "" "http://localhost:5215"
)
echo Listo.
