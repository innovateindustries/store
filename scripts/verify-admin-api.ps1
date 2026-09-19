# Verificacion staff de api/admin (requiere credenciales CEO/FOUNDER).
# Uso: powershell -ExecutionPolicy Bypass -File scripts/verify-admin-api.ps1 -User X -Pass Y [-BaseUrl http://localhost:5215]
param([string]$User = '', [string]$Pass = '', [string]$BaseUrl = 'http://localhost:5215')

$ErrorActionPreference = 'Stop'
$B = $BaseUrl.TrimEnd('/')
if ([string]::IsNullOrWhiteSpace($User)) {
    $credFile = Join-Path $env:TEMP 'smoke-creds.json'
    if (Test-Path -LiteralPath $credFile) {
        $c = Get-Content -LiteralPath $credFile -Raw | ConvertFrom-Json
        $User = $c.user
        $Pass = $c.pass
    }
}
$nOk = 0
$nFail = 0
function Check($name, $cond, $extra = '') {
    if ($cond) { $script:nOk++; Write-Output ("OK   " + $name) }
    else { $script:nFail++; Write-Output ("FALLO " + $name + " " + $extra) }
}
function Req($method, $rel, $body = $null, $token = $null) {
    $h = @{}
    if ($token) { $h['Authorization'] = 'Bearer ' + $token }
    $p = @{ Uri = $B + '/' + $rel; Method = $method; Headers = $h; TimeoutSec = 15 }
    if ($body -ne $null) {     $p['Body'] = ($body | ConvertTo-Json -Depth 4 -Compress); $p['ContentType'] = 'application/json' }
    try {
        $r = Invoke-WebRequest @p -UseBasicParsing
        return @{ code = $r.StatusCode; json = ($r.Content | ConvertFrom-Json) }
    } catch {
        $resp = $_.Exception.Response
        if ($resp -and $resp.StatusCode) {
            $sr = New-Object IO.StreamReader($resp.GetResponseStream())
            $txt = $sr.ReadToEnd()
            $j = $null
            try { $j = $txt | ConvertFrom-Json } catch { }
            return @{ code = [int]$resp.StatusCode; json = $j; raw = $txt }
        }
        return @{ code = -1; json = $null; raw = $_.Exception.Message }
    }
}

$r = Req 'POST' 'api/launcher/login' @{ identifier = $User; password = $Pass }
$tok = if ($r.json) { $r.json.Token } else { '' }
Check 'login staff' ($r.code -eq 200 -and $tok.Length -gt 20)

$r = Req 'GET' 'api/admin/users' -token $tok
Check 'admin/users' ($r.code -eq 200)

$r = Req 'POST' 'api/admin/invite-keys' -token $tok
$ik = if ($r.json) { $r.json.code } else { '' }
Check 'invite-key nueva' ($r.code -eq 200 -and $ik.StartsWith('INN-')) ("code=" + $r.code)

$r = Req 'GET' 'api/admin/invite-keys' -token $tok
$iid = -1
if ($r.json -and $r.json.keys) { foreach ($k in $r.json.keys) { if ($k.code -eq $ik) { $iid = $k.id } } }
Check 'invite-key listada' ($iid -gt 0)

if ($iid -gt 0) {
    $r = Req 'POST' ("api/admin/invite-keys/" + $iid + "/deactivate") -token $tok
    Check 'invite-key off' ($r.code -eq 200)
    $r = Req 'DELETE' ("api/admin/invite-keys/" + $iid) -token $tok
    Check 'invite-key del' ($r.code -eq 200)
}

$r = Req 'POST' 'api/admin/founder-keys' -token $tok
$fk = if ($r.json) { $r.json.code } else { '' }
Check 'founder-key nueva' ($r.code -eq 200 -and $fk.StartsWith('FND-'))

$r = Req 'GET' 'api/admin/founder-keys' -token $tok
$fid = -1
if ($r.json -and $r.json.keys) { foreach ($k in $r.json.keys) { if ($k.code -eq $fk) { $fid = $k.id } } }
Check 'founder-key listada' ($fid -gt 0)

if ($fid -gt 0) {
    $r = Req 'DELETE' ("api/admin/founder-keys/" + $fid) -token $tok
    Check 'founder-key del' ($r.code -eq 200)
}

$r = Req 'GET' 'api/admin/builds' -token $tok
Check 'builds' ($r.code -eq 200)

$r = Req 'GET' 'api/admin/news' -token $tok
Check 'news lista' ($r.code -eq 200)

$r = Req 'POST' 'api/admin/news' @{ title = 'Smoke'; body = 'cuerpo' } -token $tok
Check 'news crear' ($r.code -eq 200)

$r = Req 'GET' 'api/admin/news' -token $tok
$nid = -1
if ($r.json -and $r.json.news) { foreach ($n in $r.json.news) { if ($n.title -eq 'Smoke') { $nid = $n.id } } }
Check 'news listada' ($nid -gt 0)

if ($nid -gt 0) {
    $r = Req 'POST' ("api/admin/news/" + $nid + "/toggle") -token $tok
    Check 'news toggle' ($r.code -eq 200)
    $r = Req 'DELETE' ("api/admin/news/" + $nid) -token $tok
    Check 'news del' ($r.code -eq 200)
}

$r = Req 'POST' 'api/admin/games/publish' @{ title = 'x' } -token $tok
Check 'publish incompleto 400' ($r.code -eq 400)

Write-Output ("--- pass=" + $nOk + " fail=" + $nFail + " ---")
if ($nFail -gt 0) { exit 1 }
