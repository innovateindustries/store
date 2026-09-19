# Fija la API que usa el panel admin estatico (docs/api.json).
# Uso: powershell -ExecutionPolicy Bypass -File scripts/set-pages-api.ps1 [-ApiBase "https://xxx.trycloudflare.com"]
# Sin -ApiBase reutiliza el ultimo tunel guardado o http://localhost:5215.
param([string]$ApiBase = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ApiBase)) {
    $guardada = Join-Path $env:TEMP 'innovate-public-url.txt'
    if (Test-Path -LiteralPath $guardada) {
        $ApiBase = ((Get-Content -LiteralPath $guardada | Select-Object -First 1) + '').Trim()
    }
    if ([string]::IsNullOrWhiteSpace($ApiBase)) { $ApiBase = 'http://localhost:5215' }
}
$ApiBase = $ApiBase.Trim().TrimEnd('/')
if ($ApiBase -notmatch '^https?://') { throw 'ApiBase debe empezar con http:// o https://' }
$json = '{"apiBase": "' + $ApiBase + '"}'
[IO.File]::WriteAllText((Join-Path $root 'docs/api.json'), $json, (New-Object Text.UTF8Encoding $false))
Write-Output ("API del panel: " + $ApiBase)
Write-Output 'Haz push para que Pages lo sirva (tarda 1-2 min).'
