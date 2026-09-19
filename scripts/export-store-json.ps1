# El launcher lo lee como catalogo de solo lectura cuando no hay API.
# Requiere el servidor local en http://localhost:5215. Regenerar al publicar juegos.
param([string]$BaseUrl = 'http://localhost:5215')

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + '/api/launcher/store/games') -TimeoutSec 20 -UseBasicParsing
$d = $r.Content | ConvertFrom-Json
$doc = [ordered]@{
    exportedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    source = 'INNOVATE INDUSTRIES WEB STORE /api/launcher/store/games'
    games = @($d.games)
}
$out = Join-Path $root 'docs/store.json'
$json = ($doc | ConvertTo-Json -Depth 6)
[IO.File]::WriteAllText($out, $json, (New-Object Text.UTF8Encoding $false))
Write-Output ("store.json OK (" + $d.games.Count + " juegos)")

# Manifiesto del launcher (publico, para el panel admin estatico + auto-update).
try {
    $m = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + '/api/launcher/client/manifest') -TimeoutSec 20 -UseBasicParsing
    [IO.File]::WriteAllText((Join-Path $root 'docs/client.json'), $m.Content, (New-Object Text.UTF8Encoding $false))
    Write-Output 'client.json OK'
} catch {
    Write-Output 'client.json omitido (sin builds publicados)'
}

# Noticias publicadas (para la portada estatica).
try {
    $n = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + '/api/news/public') -TimeoutSec 20 -UseBasicParsing
    [IO.File]::WriteAllText((Join-Path $root 'docs/news.json'), $n.Content, (New-Object Text.UTF8Encoding $false))
    Write-Output 'news.json OK'
} catch {
    Write-Output 'news.json omitido'
}
