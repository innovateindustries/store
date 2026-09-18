# Cloudflare para WEB + TIENDA + LAUNCHER (ASP.NET MVC, no Workers)

> Este proyecto NO se migra a Workers. Cloudflare va DELANTE del hosting .NET como CDN + WAF + DNS + Tunnel.

## Arquitectura objetivo

```
Jugador/Cliente
  → Cloudflare Edge (CDN cache, WAF, Turnstile, TLS, reglas)
  → Tunnel `cloudflared` o hosting público (IIS / VPS / Azure)
    → ASP.NET MVC :5215 (`Program.cs`, `app.db`)
  → APIs externas: itch.io, webbun.github.io, decapi.me/twitch, instagram.com
 estático: `Publish/index.html` → Cloudflare Pages (landing/portada)
 subidas: `wwwroot/uploads/news/` → hoy disco, mañana R2 + CDN
```

## Qué skill usar y cuándo
- `cloudflare` (oficial `cloudflare/skills`): decidir producto, Turnstile, reglas cache/WAF, Tunnel. Instalación: `npx skills add https://github.com/cloudflare/skills`
- `wrangler`: solo para Pages/Tunnel/R2/D1 periféricos, nunca para reescribir la app.
- `web-perf`: auditar LCP/CLS/INP de `_Layout.cshtml` + `site.css` + Three.js.
- `turnstile-spin`: añadir captcha invisible a Login/Register/InternalRegister sin romper Identity.
- `cloudflare-one` / Tunnel: exponer dev `localhost:5000` al launcher para probar `api/launcher` sin abrir puertos.

## Recetas

### 1. Servidor local fijo SIN túnel (por defecto: launcher en el mismo PC)
El servidor corre en puerto fijo `http://localhost:5215` vía `servidor-watchdog.bat`
(supervisor con auto-reintento) + acceso directo `InnovateStore` en Inicio de Windows.
El launcher apunta SIEMPRE a `http://localhost:5215` — la URL nunca cambia, no hay túnel.
- Arrancar: doble clic a `start-server.bat` · Detener: `stop-server.bat`
- Tras `dotnet publish -c Release`, el watchdog sirve el nuevo publicado en el siguiente reintento.
- Log: `%TEMP%\innovate-store.log` · Diagnóstico: `scripts\check-procs.ps1`
- El watchdog convive con VS (F5): si el puerto está ocupado, espera; al cerrar VS, toma el relevo.

Solo usar túnel si el launcher está en OTRO PC/red (acceso externo puntual):
```bash
cloudflared tunnel --url http://localhost:5215
# usar la URL https resultante como base en el launcher para register/login/ping/me/heartbeat/session/end
```
No commitear la URL. Caché externa sigue 5/2/10 min según `Program.cs:35-65`.
Para URL externa estable (no efímera): `cloudflared tunnel login` + túnel nombrado + DNS fijo (requiere dominio en Cloudflare).

### 2. Portada estática a Pages
```bash
dotnet publish -c Release
npx wrangler pages deploy ./Publish --project-name innovate-store --branch main
```
`index.html` ya se copia por MSBuild. En Pages: `Cache-Control: public, max-age=300`, HTTPS auto, dominio custom después.

### 3. Tienda: R2 para `uploads/news` (cuando el disco se quede corto)
- Crear bucket `innovate-news`, dominio CDN `cdn.tudominio.com`.
- Subir JPG/PNG/GIF/WEBP ≤5 MB, servir por CDN, guardar URL en `NewsItems.ImagePath`.
- Mantener borrado de archivo al eliminar noticia (hoy local, mañana `S3 DeleteObject` a R2).
- Futuro agente-compras: exponer `/llms.txt` + `GET /api/products` estilo `cloudflare/templates/commerce-llms-txt-template` (ver `SKILLS-REPOS.md`).

### 4. Seguridad mínima rentable
- WAF: bloquear `POST /Account/*` con rate-limit 10/min/IP + Managed Rules ON.
- Turnstile en Login/Register: validar `siteverify` en servidor antes de `SignInManager`.
- `AllowedHosts` + `Request.Host` para Twitch `parent` (ya implementado, no hardcodear).
- Cache Rules: NUNCA cachear `/Account/*`, `/Admin/*`, `/api/launcher/*`. Cachear `/`, `/Home/*` 5 min, estáticos `/_framework/*`, `/css/*`, `/js/*`, `/lib/*` 1 año + hash.

### 5. Launcher
- `LauncherApiController` + `LauncherModels` quedan como están. Cloudflare solo da TLS + DDoS.
- Versionado: endpoint `ping` devuelve versión mínima; si el launcher es viejo, redirigir a `/Plataforma` (itch.io).
- Descargas grandes (.zip del juego): pasar por R2 con links firmados, no por el MVC.

## Checklist prod
- [ ] Dominio en Cloudflare (naranjita ON), TLS Full Strict
- [ ] Tunnel o hosting con HTTPS real (`UseHttpsRedirection` + `UseHsts` ya en `Program.cs:131-139`)
- [ ] Reglas cache/WAF arriba aplicadas
- [ ] `Publish/index.html` desplegado en Pages y apuntado a `/`
- [ ] Logs: `wrangler tail` (Pages) + `dotnet` stdout (MVC) + observability MCP
