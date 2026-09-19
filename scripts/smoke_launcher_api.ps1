# Smoke test del backend canonico (Web Store) para el INNOVATE LAUNCHER.
# Uso: powershell -ExecutionPolicy Bypass -File scripts/smoke_launcher_api.ps1 [-BaseUrl http://localhost:5215]
# Crea usuarios temporales smoke_* (restaurar app.db desde backup despues).
param([string]$BaseUrl = 'http://localhost:5215')

$ErrorActionPreference = 'Stop'

$B = $BaseUrl.TrimEnd('/')
$pass = 0
$fail = 0
function Check($name, $cond, $extra = '') {
    if ($cond) { $script:pass++; Write-Output ("OK   " + $name) }
    else { $script:fail++; Write-Output ("FALLO " + $name + " " + $extra) }
}
function Req($method, $rel, $body = $null, $token = $null) {
    $h = @{}
    if ($token) { $h['Authorization'] = 'Bearer ' + $token }
    $p = @{ Uri = $B + '/' + $rel; Method = $method; Headers = $h; TimeoutSec = 15 }
    if ($body -ne $null) { $p['Body'] = ($body | ConvertTo-Json -Compress); $p['ContentType'] = 'application/json' }
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

$suf = [Guid]::NewGuid().ToString('N').Substring(0, 6)
$u1 = 'smoke_' + $suf
$e1 = $u1 + '@test.local'
$uf = 'smokef_' + $suf
$ef = $uf + '@test.local'

$r = Req 'GET' 'api/launcher/ping'
Check 'ping' ($r.code -eq 200)

$r = Req 'POST' 'api/launcher/register' @{ username = $u1; email = $e1; password = 'Test1234!'; plan = 'USUARIO' }
Check 'register 201' ($r.code -eq 201)

$r = Req 'POST' 'api/launcher/login' @{ identifier = $u1; password = 'Test1234!' }
$tok1 = if ($r.json) { $r.json.Token } else { '' }
Check 'login token' ($r.code -eq 200 -and $tok1.Length -gt 20)

$r = Req 'GET' 'api/launcher/me' -token $tok1
Check 'me' ($r.code -eq 200)

$r = Req 'POST' 'api/launcher/register-founder' @{ username = $uf; email = $ef; password = 'Test1234!'; key = 'FND-MALA' }
Check 'founder key mala 400' ($r.code -eq 400)

$r = Req 'POST' 'api/launcher/register-founder' @{ username = $uf; email = $ef; password = 'Test1234!'; key = 'FND-SMOKE01' }
Check 'founder key valida 201' ($r.code -eq 201) ("code=" + $r.code)

$r = Req 'POST' 'api/launcher/login' @{ identifier = $uf; password = 'Test1234!' }
$tokF = if ($r.json) { $r.json.Token } else { '' }
$rolesF = if ($r.json -and $r.json.Roles) { ($r.json.Roles -join ',') } else { '' }
Check 'founder login rol FOUNDER' ($r.code -eq 200 -and $rolesF -match 'FOUNDER') ("roles=" + $rolesF)

$r = Req 'POST' 'api/launcher/register-founder' @{ username = ($uf + 'x'); email = ('x' + $ef); password = 'Test1234!'; key = 'FND-SMOKE01' }
Check 'founder key un solo uso 400' ($r.code -eq 400)

$r = Req 'GET' 'api/admin/users' -token $tok1
Check 'admin/users sin staff 403' ($r.code -eq 403)

$r = Req 'GET' 'api/admin/users' -token $tokF
$usersN = if ($r.json -and $r.json.users) { $r.json.users.Count } else { -1 }
Check 'admin/users staff 200' ($r.code -eq 200 -and $usersN -ge 2) ("n=" + $usersN)

$r = Req 'GET' 'api/launcher/points' -token $tok1
Check 'points 0' ($r.code -eq 200 -and $r.json.points -eq 0)

$r = Req 'POST' 'api/admin/points/grant/smoke_noexiste' @{ amount = 100 } -token $tokF
Check 'grant usuario inexistente 404' ($r.code -eq 404)

$r = Req 'POST' ("api/admin/points/grant/" + $u1) @{ amount = 500 } -token $tokF
Check 'grant 500' ($r.code -eq 200 -and $r.json.points -eq 500) ("pts=" + $r.json.points)

$r = Req 'GET' 'api/launcher/rewards' -token $tok1
$rw = if ($r.json -and $r.json.rewards) { $r.json.rewards.Count } else { -1 }
Check 'rewards 5' ($r.code -eq 200 -and $rw -eq 5) ("n=" + $rw)

$r = Req 'POST' 'api/launcher/rewards/redeem' @{ rewardId = 'coin_100' } -token $tok1
Check 'redeem sin saldo 402' ($r.code -eq 402)

$r = Req 'POST' 'api/launcher/rewards/redeem' @{ rewardId = 'no_existe' } -token $tok1
Check 'redeem premio inexistente 404' ($r.code -eq 404)

$r = Req 'GET' 'api/launcher/profile' -token $tok1
Check 'profile' ($r.code -eq 200)

$r = Req 'GET' 'api/launcher/store/games'
Check 'store/games' ($r.code -eq 200)

$r = Req 'POST' 'api/launcher/store/order' @{ gameId = 999999 } -token $tok1
Check 'order juego inexistente 400' ($r.code -eq 400)

$r = Req 'GET' 'api/launcher/store/orders' -token $tok1
Check 'orders vacias' ($r.code -eq 200)

$r = Req 'GET' 'api/launcher/games/999999/manifest' -token $tok1
Check 'manifest juego inexistente 404' ($r.code -eq 404)

$r = Req 'GET' 'api/launcher/client/manifest'
Check 'client/manifest' ($r.code -eq 200 -or $r.code -eq 404) ("code=" + $r.code)

$r = Req 'GET' 'api/admin/orders' -token $tokF
Check 'admin/orders' ($r.code -eq 200)

$r = Req 'POST' 'api/admin/orders/999999/confirm' @{} -token $tokF
Check 'confirm inexistente 404' ($r.code -eq 404)

$r = Req 'POST' 'api/admin/games/publish' @{ title = 'x' } -token $tokF
Check 'publish incompleto 400' ($r.code -eq 400)

$r = Req 'POST' 'api/launcher/heartbeat' @{ gameSlug = 'smoke'; gameTitle = 'Smoke' } -token $tok1
Check 'heartbeat' ($r.code -eq 200)

$r = Req 'POST' 'api/launcher/session/end' @{ gameSlug = 'smoke'; gameTitle = 'Smoke' } -token $tok1
Check 'session/end' ($r.code -eq 200)

Write-Output ("--- pass=" + $pass + " fail=" + $fail + " ---")
if ($fail -gt 0) { exit 1 }
