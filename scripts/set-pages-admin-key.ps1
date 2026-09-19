# Fija la key del panel admin estatico (docs/admin.html).
# Guarda solo el SHA-256 en el HTML (la key en claro no se commitea).
# Uso: powershell -ExecutionPolicy Bypass -File scripts/set-pages-admin-key.ps1 [-Key "MI-KEY"]
# Sin -Key genera una aleatoria y la muestra (guardala: no se puede recuperar).
param([string]$Key = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$file = Join-Path $root 'docs/admin.html'
if (-not (Test-Path -LiteralPath $file)) { throw 'No existe docs/admin.html' }

if ([string]::IsNullOrWhiteSpace($Key)) {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $b = New-Object byte[] 9
    $rng.GetBytes($b)
    $hex = ($b | ForEach-Object { $_.ToString('X2') }) -join ''
    $Key = 'ADM-' + $hex.Substring(0, 4) + '-' + $hex.Substring(4, 4) + '-' + $hex.Substring(8, 4)
}
$Key = $Key.Trim()
$bytes = [Text.Encoding]::UTF8.GetBytes($Key)
$hash = [Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
$hexHash = ($hash | ForEach-Object { $_.ToString('x2') }) -join ''

$html = [IO.File]::ReadAllText($file, [Text.Encoding]::UTF8)
if ($html.Contains('__ADMIN_KEY_SHA256__')) {
    $html = $html.Replace('__ADMIN_KEY_SHA256__', $hexHash)
} else {
    $html = [regex]::Replace($html, 'var KEY_HASH = "[0-9a-f]{64}"', ('var KEY_HASH = "' + $hexHash + '"'))
}
[IO.File]::WriteAllText($file, $html, (New-Object Text.UTF8Encoding $false))
Write-Output 'Key del panel admin actualizada (solo hash en el HTML).'
Write-Output ("TU KEY (guardala, no se commitea): " + $Key)
