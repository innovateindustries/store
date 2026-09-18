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
    @('/', 'index.html'),
    @('/Home/Store', 'store.html'),
    @('/Home/Plataforma', 'plataforma.html'),
    @('/Home/Noticias', 'noticias.html'),
    @('/Home/Privacy', 'privacidad.html'),
    @('/Account/Login', 'login.html'),
    @('/Account/Register', 'registro.html'),
    @('/Account/Plans', 'planes.html'),
    @('/Account/InternalKey', 'interno.html'),
    @('/Account/InternalRegister', 'registro-interno.html')
)
# Showcase se construye aparte: en anonimo el filtro [CategoryEnabled] redirige
# a Ajustes, asi que el GET directo no trae el player de Twitch.

$banner = '<div class="static-banner">VISTA EST&Aacute;TICA DEMOSTRATIVA &#9679; la app interactiva (cuentas, compras, descargas, admin) corre en local con <code>dotnet run</code> &#8594; http://localhost:5215</div>'
$bannerCss = '<style>.static-banner{margin:0 0 16px;padding:10px 14px;text-align:center;font-size:.8rem;letter-spacing:1px;color:#ffd166;border:1px dashed rgba(255,209,102,.55);border-radius:8px;background:rgba(255,209,102,.06)}.static-banner code{color:#05ffa1;font-size:.8rem}</style></head>'

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
    $h = $h -replace 'href="/Account/Login"', 'href="login.html"'
    $h = $h -replace 'href="/Account/Register"', 'href="registro.html"'
    $h = $h -replace 'href="/Account/Plans"', 'href="planes.html"'
    $h = $h -replace 'href="/Account/InternalKey"', 'href="interno.html"'
    $h = $h -replace 'href="/Account/InternalRegister"', 'href="registro-interno.html"'
    $h = $h -replace 'href="/Account/Ajustes"', 'href="login.html"'
    $h = $h -replace 'href="/Admin"', 'href="login.html"'
    $h = $h -replace 'href="/Account/Logout"', 'href="login.html"'
    $h = $h -replace 'href="/"', 'href="index.html"'
    # Absolutas del sitio -> relativas (proyecto Pages vive en /store/)
    $h = $h -replace 'href="/', 'href="./'
    $h = $h -replace 'src="/', 'src="./'
    $h = $h -replace 'action="/Account/(Login|Register|InternalKey|InternalRegister)"', 'action="#"'
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
            '<a class="nav-link" data-i18n="nav.noticias" href="noticias.html">Noticias</a></li><li class="nav-item"><a class="nav-link" data-i18n="nav.showcase" href="showcase.html">Showcase</a>'
    }
    # CSS del banner (solo la usa el aviso de formularios) tras </head>
    # (Razor inyecta atributos de CSS con ambito tipo b-xxxxxxx: no asumir tag exacto)
    $h = $h -replace '</head>', $bannerCss
    # Formularios: en Pages no hay backend; interceptar el envio y explicarlo
    # en vez de caer en un 404 (login/registro solo existen en la app local).
    $formJs = '<script>document.addEventListener("submit",function(e){var f=e.target;if(f&&f.tagName==="FORM"){e.preventDefault();var n=document.getElementById("static-form-msg");if(!n){n=document.createElement("div");n.id="static-form-msg";n.className="static-banner";f.parentNode.insertBefore(n,f);}n.innerHTML="Las cuentas solo funcionan en la app local: <code>dotnet run</code> &#8594; http://localhost:5215";if(n.scrollIntoView){n.scrollIntoView();}}});</script></body>'
    if ($h -match '<form') {
        $h = $h -replace '</body>', $formJs
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

# Showcase: mismo layout + contenido real de Views/Home/Showcase.cshtml
# (canal tarloox, parent del dominio Pages, estado desconocido en estatico).
$showcaseBody = @'
<section class="hero-neon" style="padding-top:50px">
    <span class="badge-neon"><span class="pulse-dot"></span><span data-i18n="sc.badge">En vivo &#183; Twitch &#183; directo</span></span>
    <h1 class="section-title mt-3"><small data-i18n="sc.eyebrow">// Showcase en Twitch</small><span data-i18n="sc.ta">Showcase</span> <span class="text-magenta-neon" data-i18n="sc.tb">en vivo</span></h1>
    <p class="lead-muted" data-i18n="sc.lead">El directo de INNOVATE INDUSTRIES en Twitch, sin salir de aqu&#237;. Si estamos en vivo lo ves abajo; si no, el player avisa y puedes seguirnos para no perd&#233;rtelo.</p>
    <div class="d-flex flex-wrap align-items-center gap-3 mt-3">
        <span class="badge-neon" style="border-color:rgba(178,183,199,.4);color:var(--text-secondary)"><span data-i18n="sc.live_unknown">&#9675; Estado del directo no disponible</span></span>
    </div>
    <div class="d-flex flex-wrap gap-3 mt-3">
        <a class="btn-neon btn-neon-magenta" href="https://www.twitch.tv/tarloox" target="_blank" rel="noopener" data-i18n="sc.follow">Seguir en Twitch &#8599;</a>
        <a class="btn-neon-outline" href="index.html" data-i18n="sc.home">&#8592; Inicio</a>
    </div>
</section>
<div class="row g-4">
    <div class="col-lg-8">
        <div class="neon-card p-2 h-100">
            <div class="ratio ratio-16x9">
                <iframe src="https://player.twitch.tv/?channel=tarloox&parent=innovateindustries.github.io&muted=true" title="Twitch live: tarloox" allowfullscreen loading="lazy" style="border:0;border-radius:8px"></iframe>
            </div>
        </div>
    </div>
    <div class="col-lg-4">
        <div class="neon-card p-3 h-100">
            <span class="card-tag tag-magenta" data-i18n="sc.chat">Chat en vivo</span>
            <iframe src="https://www.twitch.tv/embed/tarloox/chat?parent=innovateindustries.github.io&darkpopout" title="Twitch chat" loading="lazy" style="border:0;border-radius:8px;width:100%;height:480px"></iframe>
            <a class="btn-neon-outline w-100 text-center mt-2" href="https://www.twitch.tv/tarloox" target="_blank" rel="noopener" data-i18n="sc.open">Abrir en Twitch &#8599;</a>
        </div>
    </div>
</div>
'@
$shell = [IO.File]::ReadAllText((Join-Path $docs 'index.html'))
$openTag = [regex]::Match($shell, '<main[^>]*>').Value
$rxMain = New-Object regex('<main[^>]*>.*</main>', 'Singleline')
$show = $rxMain.Replace($shell, ($openTag + $showcaseBody + '</main>'), 1)
$show = $show -replace '<title>.*?</title>', '<title>Showcase - INNOVATE INDUSTRIES WEB STORE (vista est&#225;tica)</title>'
[IO.File]::WriteAllText((Join-Path $docs 'showcase.html'), $show, [Text.UTF8Encoding]::new($false))
Write-Output 'OK showcase.html (estatico desde la vista real)'
Write-Output 'Snapshot listo en docs/'
