@echo off
REM Supervisor INNOVATE STORE: http://localhost:5215 fijo (SIN tunel).
REM - Si el puerto responde, no hace nada (espera 20 s). Asi convive con VS (F5):
REM   mientras depuras, el watchdog queda en espera; al cerrar VS, toma el relevo solo.
REM - Si el puerto esta libre, arranca el publicado Release; si se cae, lo relanza a los 5 s.
REM Se ejecuta minimizado desde el acceso directo "InnovateStore" en Inicio de Windows.
cd /d "%~dp0"
:loop
curl -s -o nul http://localhost:5215/api/launcher/ping
if not errorlevel 1 (
  timeout /t 20 /nobreak >nul
  goto loop
)
echo [%date% %time%] puerto libre, arrancando servidor >> "%TEMP%\innovate-store.log"
dotnet "bin\Release\net10.0\publish\INNOVATE INDUSTRIES WEB STORE.dll" --urls http://localhost:5215 >> "%TEMP%\innovate-store.log" 2>&1
echo [%date% %time%] el servidor termino, reintentando en 5 s >> "%TEMP%\innovate-store.log"
timeout /t 5 /nobreak >nul
goto loop
