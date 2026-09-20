# Sincroniza el admin local con GitHub Pages: exporta JSONs, regenera docs
# (snapshot + uploads) y sube SOLO docs/ si hubo cambios.
# Uso manual: powershell -ExecutionPolicy Bypass -File scripts/sync-pages.ps1
# Automatico: scripts/install-sync-task.ps1 (cada 5 min).
param([string]$CommitMsg = 'Sync Pages (auto)')

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
$log = Join-Path $env:TEMP 'innovate-sync.log'
function Log($m) {
    $line = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') + ' ' + $m
    Add-Content -LiteralPath $log -Value $line
}

# 1) El servidor debe responder (si no, no hay nada que exportar).
try {
    $p = Invoke-WebRequest -Uri 'http://localhost:5215/api/launcher/ping' -TimeoutSec 10 -UseBasicParsing
    if ($p.StatusCode -ne 200) { Log 'sin servidor, se omite'; exit 0 }
} catch { Log 'sin servidor, se omite'; exit 0 }

# 2) Exportar JSONs + regenerar snapshot.
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'scripts/export-store-json.ps1') | Out-Null
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'scripts/snapshot-pages.ps1') | Out-Null

# 3) Subir solo docs/ si cambio algo.
git add -A -- docs 2>&1 | Out-Null
$st = git status --porcelain -- docs 2>&1 | Out-String
if ([string]::IsNullOrWhiteSpace($st)) { Log 'sin cambios'; exit 0 }
git -c user.name="innovateindustries" -c user.email="innovativeindustriesoficial@gmail.com" commit -m $CommitMsg 2>&1 | Out-Null
$push = git push origin main 2>&1 | Out-String
if ($LASTEXITCODE -eq 0) { Log 'push OK' } else { Log ('push FALLO: ' + $push) }
