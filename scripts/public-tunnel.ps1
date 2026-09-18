$ErrorActionPreference = 'Stop'
# Expone http://localhost:5215 a internet con URL publica temporal (quick tunnel).
# Para URL FIJA (no efimera): cloudflared login + tunel nombrado + dominio (ver .agent/CLOUDFLARE-WEB-TIENDA-LAUNCHER.md).
$cloudflared = 'C:\Program Files (x86)\cloudflared\cloudflared.exe'
$log = Join-Path $env:TEMP 'cloudflared-tunnel.log'
$urlFile = Join-Path $env:TEMP 'innovate-public-url.txt'
# 1) Si hay URL guardada y responde, se REUTILIZA (mismo link, no se crea otro tunel).
if (Test-Path -LiteralPath $urlFile) {
    $old = ((Get-Content -LiteralPath $urlFile | Select-Object -First 1) + '').Trim()
    if ($old) {
        try {
            $chk = Invoke-WebRequest -Uri ($old + '/api/launcher/ping') -TimeoutSec 10 -UseBasicParsing
            if ($chk.StatusCode -eq 200) { Write-Output "URL PUBLICA (mismo link): $old"; exit 0 }
        } catch {}
    }
}
# 2) Tunel muerto o sin respuesta: matar restos y crear uno nuevo (link nuevo, inevitable sin dominio).
Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Start-Sleep -Seconds 2
if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log -Force }
$cmd = "`"$cloudflared`" tunnel --url http://localhost:5215 --logfile `"$log`""
$r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $cmd }
Write-Output "cloudflared lanzado (PID $($r.ProcessId)). Esperando URL publica..."
$url = $null
for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 2
    if (Test-Path -LiteralPath $log) {
        $m = Select-String -Path $log -Pattern 'https://[a-zA-Z0-9-]+\.trycloudflare\.com' | Select-Object -First 1
        if ($m) { $url = $m.Matches.Value; break }
    }
}
if ($url) {
    Write-Output "URL PUBLICA: $url"
    Set-Content -LiteralPath (Join-Path $env:TEMP 'innovate-public-url.txt') -Value $url
}
else { Write-Output 'NO_URL_TODAVIA: revisa el log en $env:TEMP\cloudflared-tunnel.log'; exit 1 }
