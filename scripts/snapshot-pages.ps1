$ErrorActionPreference = 'Stop'
# Snapshot estatico de la tienda real para GitHub Pages (docs/).
# Requiere el servidor local en http://localhost:5215 (publicado Release o dotnet run).
# La app interactiva (login, compras, admin, api/launcher) NO corre en Pages:
# esto publica la vitrina visual real (mismo layout/CSS/datos).
# NOTA: este archivo es 100% ASCII a proposito (PS 5.1 lee .ps1 sin BOM como ANSI);
# las tildes van como entidades HTML.

$root = Split-Path -Parent $PSScriptRoot
$docs = Join-Path $root 'docs'
$base = 'http://localhost:5215'
$pagesHost = 'innovateindustries.github.io'

$pages = @(
    @('/Home/Privacy', 'privacidad.html')
)
# index.html, store.html, noticias.html, plataforma.html y showcase.html
# son NEXUS hechos a mano, NO se regeneran. Solo privacidad sale del snapshot.
# Sin cuentas en Pages (a peticion): no se generan login/registro/planes/interno.
# El nav anonimo trae Entrar/Crear cuenta/Ajustes: se eliminan del snapshot.
# Showcase se construye aparte: en anonimo el filtro [CategoryEnabled] redirige
# a Ajustes, asi que el GET directo no trae el player de Twitch.

foreach ($p in $pages) {
    $url = $base + $p[0]
    $out = Join-Path $docs $p[1]
    try {
        $r = Invoke-WebRequest -Uri $url -TimeoutSec 20 -UseBasicParsing
    } catch {
        Write-Output ("AVISO: no se pudo descargar $url : " + $_.Exception.Message)
        continue
    }
    $h = $r.Content
    # Rutas MVC -> archivos estaticos (orden: especificas primero)
    $h = $h -replace 'href="/Home/Store"', 'href="store.html"'
    $h = $h -replace 'href="/Home/Plataforma"', 'href="plataforma.html"'
    $h = $h -replace 'href="/Home/Showcase"', 'href="showcase.html"'
    $h = $h -replace 'href="/Home/Noticias"', 'href="noticias.html"'
    $h = $h -replace 'href="/Home/Privacy"', 'href="privacidad.html"'
    $h = $h -replace 'href="/"', 'href="index.html"'
    # Absolutas del sitio -> relativas (proyecto Pages vive en /store/)
    $h = $h -replace 'href="/', 'href="./'
    $h = $h -replace 'src="/', 'src="./'
    # Sin cuentas en Pages: se eliminan del nav Entrar / Crear cuenta / Ajustes
    # (los <li> traen atributo de CSS con ambito b-xxxxxxx: no asumir tag exacto)
    $h = $h -replace '<li[^>]*>\s*<a class="nav-link" data-i18n="nav.ajustes"[^<]*</a>\s*</li>', ''
    $h = $h -replace '<li[^>]*>\s*<a class="nav-link" data-i18n="acc.login_link"[^<]*</a>\s*</li>', ''
    $h = $h -replace '<li[^>]*>\s*<a class="btn btn-sm btn-neon" data-i18n="acc.register_link"[^<]*</a>\s*</li>', ''
    # Twitch embed: parent localhost no vale en Pages
    $h = $h -replace 'parent=localhost[^"&]*', ('parent=' + $pagesHost)
    # Descarga del launcher: accion MVC -> zip real copiado a docs/uploads
    $zip = Get-ChildItem -Path (Join-Path $root 'wwwroot/uploads/launcher') -Filter '*.zip' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($zip) {
        $h = $h -replace 'href="\./Home/DescargarLauncher"', ('href="./uploads/launcher/' + $zip.Name + '"')
    }
    # En Pages no hay login: el nav anonimo oculta Showcase; se expone igual
    if ($h -notmatch 'showcase\.html') {
        $h = $h -replace '<a class="nav-link" data-i18n="nav.noticias" href="noticias.html">Noticias</a>',
            '<a class="nav-link" data-i18n="nav.noticias" href="noticias.html">Noticias</a></li><li class="nav-item"><a class="nav-link" data-i18n="nav.showcase" href="showcase.html">Showcase</a></li><li class="nav-item"><a class="nav-link" href="admin.html">Admin</a>'
    }
    # Titulo con contexto Pages
    $h = $h -replace '</title>', ' (vista est&#225;tica)</title>'
    [IO.File]::WriteAllText($out, $h, [Text.UTF8Encoding]::new($false))
    Write-Output ("OK " + $p[1] + " (" + $h.Length + " chars)")
}

# Assets minimos referenciados por _Layout
$assets = @(
    @('wwwroot/css/site.css', 'css/site.css'),
    @('wwwroot/js/site.js', 'js/site.js'),
    @('wwwroot/js/i18n.js', 'js/i18n.js'),
    @('wwwroot/favicon.ico', 'favicon.ico'),
    @('wwwroot/lib/bootstrap/dist/css/bootstrap.min.css', 'lib/bootstrap/dist/css/bootstrap.min.css'),
    @('wwwroot/lib/jquery/dist/jquery.min.js', 'lib/jquery/dist/jquery.min.js'),
    @('wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.min.js', 'lib/bootstrap/dist/js/bootstrap.bundle.min.js')
)
foreach ($a in $assets) {
    $src = Join-Path $root $a[0]
    $dst = Join-Path $docs $a[1]
    $dir = Split-Path -Parent $dst
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Copy-Item -LiteralPath $src -Destination $dst -Force
    Write-Output ("asset " + $a[1])
}

# Imagenes subidas (noticias, portadas, capturas, builds) para que la vitrina se vea completa
$upSrc = Join-Path $root 'wwwroot/uploads'
$upDst = Join-Path $docs 'uploads'
if (Test-Path -LiteralPath $upSrc) {
    if (-not (Test-Path -LiteralPath $upDst)) { New-Item -ItemType Directory -Path $upDst -Force | Out-Null }
    Copy-Item -Path (Join-Path $upSrc '*') -Destination $upDst -Recurse -Force
    Write-Output 'uploads copiados'
}

# Showcase es NEXUS hecho a mano (docs/showcase.html), NO se regenera.
Write-Output 'Snapshot listo en docs/'
