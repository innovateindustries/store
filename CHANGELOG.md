# INNOVATE INDUSTRIES WEB STORE — Bitácora de cambios

Documento vivo de la evolución del proyecto. Última actualización: checkpoint actual.

## 2026-09-19 — Backend canónico del launcher (Fases 1-2)
- **Founder Key server-side:** tabla `FounderKeys` (`FND-XXXXXXXX`, usos, expiración) +
  `POST api/launcher/register-founder` (otorga FOUNDER, validación 100% servidor) +
  Admin → keys de fundador (generar/desactivar/eliminar, solo CEO/FOUNDER).
  i18n: `adm.fkeys_t/col_uses/empty_fkeys/up_mandatory/st_mandatory` en 8 idiomas.
- **Paridad con INNOVATE.Server:** `store/order(s)`, `points/rewards/redeem`,
  `profile/photo`, `games/{id}/manifest|file` (con licencia), `api/admin/*`
  (users/orders/confirm/cancel/delete/publish/grant) y `client/manifest` (auto-update).
  Tablas: `LauncherPoints/LauncherProfiles/GameBuilds` + columnas
  `StoreGames(Genre/Regions/InPass)` + `LauncherBuilds.IsMandatory` (checkbox en Admin).
- `ToggleOrder` otorga 1 USD = 100 pts al pagar (solo primera key). Smoke
  `scripts/smoke_launcher_api.ps1`: 27/27 en vivo. Build 0 warnings.
- Catálogo público para el launcher: `scripts/export-store-json.ps1` genera
  `docs/store.json` (https://innovateindustries.github.io/store/store.json).
  Regenerar al publicar juegos.
- Panel admin estático OPERABLE desde GitHub Pages: `docs/admin.html` con login real
  (token en sesión) + tabs Usuarios (ban/desban CEO/FOUNDER), Pedidos (pagar/cancelar/
  eliminar), Keys INN/FND, Builds, Noticias y Publicar juego + Puntos. Consume la API
  vía `docs/api.json` (`scripts/set-pages-api.ps1`, localhost o túnel). CORS `Pages`.
  Endpoints nuevos: `api/admin/users/{id}/ban`, `invite-keys/*`, `founder-keys/*`,
  `builds`, `news/*`. Smoke `scripts/verify-admin-api.ps1`: 16/16 en vivo.
- Estética NEXUS oscura (no retro) en Pages: `store.html` literal al mockup (sidebar,
  hero DESTACADO, destacados/nuevos/DLC/ofertas/biblioteca, buscador) + `index.html`
  a juego (héroe, destacados, noticias). Datos vía `store.json`/`news.json`
  (`GET api/news/public` nuevo); `index/store` ya no salen del snapshot.

## 2026-09-18 — Pages: vitrina estática REAL de la tienda
- `docs/` ya no es un cartel de "ejecuta dotnet run": es un snapshot de la tienda real
  (mismo `_Layout`, `site.css`, i18n 8 idiomas, noticias y build publicados):
  `index/store/plataforma/showcase/noticias/privacidad.html` + `css/js/lib/favicon/uploads`.
- Generado con `scripts/snapshot-pages.ps1` (requiere servidor en `http://localhost:5215`):
  reescribe rutas MVC→`.html`, absolutas→relativas, Twitch `parent=innovateindustries.github.io`,
  botón Descargar→zip real de `uploads/launcher`, expone Showcase en el nav.
  Sin cuentas en Pages (a petición): fuera Entrar/Crear cuenta/Ajustes y las páginas
  `login/registro/planes/interno*.html` (el login sigue solo en local).
  Regenerar tras cada cambio visual.
- Límite honesto: sin login/compras/admin/descargas con contador ni `api/launcher` (Pages es
  estático; la app interactiva corre en local con `dotnet run` → `http://localhost:5215`).

## 2026-09-18 — Pages: portada corregida (categorías reales)
- `Publish/index.html` + `docs/index.html`: chips INICIO/STORE/PLATAFORMA/SHOWCASE/NOTICIAS/AJUSTES
  (fuera EVENTOS/ROBLOX/VLOG eliminados el 2026-09-14, entra STORE). Fuera links itch.io y Eventos
  (Plataforma sirve builds del Admin, sin itch). URL principal `http://localhost:5215` (+ `https://localhost:7009`).
- Reportado por captura del usuario en `innovateindustries.github.io/store/`.

## 2026-09-18 — GitHub Pages (link gratuito)
- Portada estática en `docs/index.html` (espejo de `Publish/index.html`) + `docs/.nojekyll`
  para activar Pages desde `main`/`docs`. URL gratuita: https://innovateindustries.github.io/store/
- Nota: Pages solo sirve la portada estática. La app completa (MVC + SQLite + Identity +
  `api/launcher`) no corre en Pages; se ejecuta con `dotnet run` o hosting .NET + Cloudflare delante.
- `README.md` con instrucciones + link Pages.

## 2026-09-15 — Admin STORE: borrar pedidos
- Tabla Pedidos: botón rojo **Eliminar** junto a Marcar pagado/pendiente. Solo CEO/FOUNDER
  (los INTERNO no lo ven). `Admin/DeleteOrder` (POST) borra el pedido con su key.
- Sin claves i18n nuevas (reutiliza `adm.btn_del` en los 8 idiomas).

## 2026-09-15 — Admin: eliminar cuentas de usuarios
- Admin → Usuarios: botón **Eliminar** en cada tarjeta (INTERNOS/USUARIOS/PUBLISHERS),
  junto a Banear/Desbanear. Solo CEO/FOUNDER, sin auto-eliminado.
- `Admin/DeleteUser` (POST): revoca sus tokens del launcher (`LauncherTokens`) y borra el
  usuario con `UserManager.DeleteAsync` (roles, claims y logins incluidos). Su historial
  (pedidos, keys, sesiones, noticias) se conserva y muestra autor "—".
- Sin claves i18n nuevas (reutiliza `adm.btn_del` en los 8 idiomas).

## 2026-09-15 — STORE: key del juego + Admin USUARIOS con bibliotecas
- Al marcar un pedido como **Pagado** (Admin → STORE → Pedidos, `ToggleOrder`) se genera
  la **key única del juego** estilo `XXXXX-XXXXX-XXXXX` (15 caracteres sin ambiguos 0/O/1/I/L
  + 2 guiones = 17). No se repite nunca (verificación contra `StoreOrders` + índice único).
  Si se devuelve a Pendiente, la key se conserva.
- El jugador ve su key en `Home/PagarOk` con botón **Copiar** (solo si el pedido ya tiene key;
  si sigue Pendiente ve el aviso de que se generará al validar el pago). La key también sale
  en la tabla de Pedidos del admin.
- Nueva página **Admin → USUARIOS** (`Admin/Usuarios`, sidebar `sb.users_lib`): lista todos los
  usuarios del launcher/web (nombre, correo, roles, nº de juegos y keys) y al hacer clic
  (`Admin/UsuarioDetalle`) muestra su biblioteca: juego, precio, **key usada**, estado y fecha.
- DB: columna `StoreOrders.KeyCode` (TEXT NULL) + índice único, migran solas por SQL crudo en
  `Program.cs` (patrón `pragma_table_info`); `DbSet` ya existía, modelo `StoreOrder.KeyCode`.
- i18n: 18 claves nuevas (`pay.key_*`, `sb.users_lib`, `adm.userslib_*`, `adm.col_user/email/roles/games/key/date`,
  `adm.btn_library`, `adm.lib_*`) en ES/EN/JA/ZH/RU/KO/PT-BR/PT-PT. Estilo `.order-ref-sm` en `site.css`.

## Estado actual
- ASP.NET Core MVC · .NET 10 · SQLite (`app.db`, se crea sola al arrancar)
- Tema neón gamer global (fondo 3D Three.js + animaciones GSAP)
- 8 idiomas con banderas (ES, EN, JA, ZH, RU, KO, PT-BR, PT-PT), persistencia en navegador
- Cuentas con Identity: registro, login (email o usuario), logout, roles
- Repo: https://github.com/innovateindustries/store

## Categorías (nav)
| Categoría | Fuente en vivo |
|---|---|
| Inicio | Contenido propio + últimas 3 noticias |
| Plataforma | Descarga del último build publicado desde Admin → LAUNCHER |
| Showcase | https://www.twitch.tv/tarloox (player + chat embebidos, estado cada 2 min) |
| Noticias | Tabla local `NewsItems` (publicadas desde Admin, sin caché) |
| Ajustes | Visibilidad por usuario: Showcase desactivada por defecto |
| LAUNCHER (Admin) | Subir builds `.zip/.exe/.msi` (máx. 512 MB) con versión + notas + publicar/ocultar + eliminar |

## Planes y equipo
- **USUARIO** (gratis): registro normal. El primer registrado sin fundador existente recibe rol FOUNDER.
- **TRABAJADOR INTERNO** (por invitación): key `INN-XXXXXXXX` de un solo uso (visible/generable solo por CEO/FOUNDER) → formulario (correo trabajo, nombres, usuario; la contraseña ES la key) → rol INTERNO → panel Admin.
- **PUBLISHER** (100 USD/publicación): marcado como Próximamente.
- Panel **Admin** (roles CEO/FOUNDER/INTERNO): sidebar Usuarios/Noticias; usuarios en 3 columnas (INTERNOS/USUARIOS/PUBLISHERS) con estado y **ban/desban** (solo CEO/FOUNDER, sin auto-baneo); keys con generar/desactivar/eliminar; noticias con imagen+links.
- Noticias: crear (imagen JPG/PNG/GIF/WEBP ≤5 MB en `wwwroot/uploads/news/`), publicar/ocultar, eliminar (borra el archivo).

## Límites conocidos de plataformas externas
- itch.io: página con contraseña (solo metadatos hasta hacerla pública) y prohíbe iframes.
- Instagram: a bots solo da cascarón vacío y bloquea iframes del perfil; se usa su widget `/embed` + verificación live.
- Twitch: embed exige `parent` exacto — se genera solo con `Request.Host` (localhost en dev, dominio en prod).

## Notas técnicas
- `app.UseStaticFiles()` explícito (requerido para servir imágenes subidas en caliente en .NET 10).
- `Publish/index.html`: portada estática que se copia a la carpeta de publicación vía target MSBuild.
- `.gitignore`: excluye `bin/`, `obj/`, `.vs/`, `*.user`, `*.db*` y secretos.
- Credenciales del fundador entregadas por chat (no están en este repo).

## Aportes en paralelo (autor: usuario, incluidos en este checkpoint)
- API del launcher `api/launcher`: register, login, ping, me, heartbeat, session/end (`Controllers/Api/LauncherApiController.cs`, `Models/LauncherModels.cs`).
- Vista admin **Horas de juego** (`Views/Admin/Horas.cshtml`).
- Cableado asociado en `Program.cs`, `AdminController.cs`, `AppDbContext.cs` y `.csproj`.

## 2026-09-14 — Admin LAUNCHER + descarga en Plataforma
- `HomeController.Plataforma` ya no consulta itch.io (fuera `FetchItchPageAsync` + helpers
  `ExtractMeta/ExtractTitleTag/CleanTitle` y el modelo `ItchPageInfo`). Vista en blanco con
  `plat.soon` en los 8 idiomas. Claves `plat.*` de itch quedan huérfanas en `i18n.js`.
  (El registro del `HttpClient "ItchIo"` en `Program.cs` queda sin uso.)
- Admin → LAUNCHER: subir builds `.zip/.exe/.msi` (máx. 512 MB) a `wwwroot/uploads/launcher/`
  con versión + notas + publicar/ocultar + eliminar (borra el archivo). Tabla `LauncherBuilds`
  (versión, notas, ruta, tamaño, descargas, fecha). Plataforma muestra el último publicado con
  botón de descarga (contador + rangos). Sin builds → "en preparación".
  Nota: subidas >100 MB solo por `http://localhost:5215` (el túnel Cloudflare gratis corta en ~100 MB).
  El formulario avisa (`adm.up_hint`) y muestra barra de progreso real durante la subida.
- Build con descripción larga + ficha técnica (una por línea, máx. 12 en Plataforma) + captura
  (JPG/PNG/GIF/WEBP ≤5 MB). Columnas migran solas con `ALTER TABLE` (`Description/Specs/ScreenshotPath`).
  Plataforma muestra descripción, captura, tarjeta de especificaciones (reusa `plat.specs`) y notas.

## 2026-09-14 — STORE: botón Pagar + pago manual con pedidos
- Cada producto tiene botón **Pagar** (no "Comprar"): en la tarjeta del grid y en la ficha
  (`store.pay` en los 8 idiomas). Requiere login (si no, redirige a `Account/Login`).
- Flujo: `Home/Pagar(id)` (resumen: carátula, título, badge, precio, comprador) →
  `ConfirmarPago` (POST) crea el pedido **Pendiente** con referencia única `ORD-XXXXXXXX` →
  `PagarOk` muestra la referencia para completar el pago manual por Discord.
- Sin pasarela conectada (Stripe/PayPal): tabla `StoreOrders` (juego, precio, referencia única,
  estado Pendiente/Pagado, comprador) por SQL crudo en `Program.cs` (+ `DbSet`).
- Admin → STORE → **Pedidos**: tabla Ref./Juego/Comprador/USD/Estado con marcar pagado/pendiente
  (`ToggleOrder`). i18n: `pay.*` (13) + `adm.orders_*`/`adm.col_*`/`adm.btn_mark*` (7) en 8 idiomas.
  Build 0 warnings.

## 2026-09-14 — categoría STORE + kit de publicación en Admin
- Nueva categoría **STORE** en el nav (`Home/Store`, siempre visible; `nav.store` en los 8 idiomas).
  Grid de juegos publicados: carátula (portada) + precio debajo + badge `PREVENTA` (magenta)
  o `ACCESO ANTICIPADO` (cyan). Ficha por juego (`Home/StoreDetalle`): descripción,
  requisitos mínimos/recomendados, hasta 6 screenshots y tráiler (embed YouTube o MP4 directo).
- Admin → **STORE** (`Admin/Store`, sidebar `sb.store`): kit de publicación con TODOS los campos
  obligatorios — portada (JPG/PNG/GIF/WEBP ≤5 MB), título, descripción, requisitos mínimos,
  requisitos recomendados, precio (USD), screenshots (1 a 6, ≤5 MB c/u) y tráiler (URL YouTube
  o MP4). Preventa y Acceso anticipado son dos preguntas excluyentes (No/No = lanzamiento normal).
  Archivos en `wwwroot/uploads/store/` (portada `cover-*`, capturas `shot-*`, screenshots como JSON).
  Publicar/ocultar + eliminar (borra portada y capturas). Tabla `StoreGames` creada por SQL crudo
  en `Program.cs` (+ `DbSet` en `AppDbContext`).
- i18n: claves `nav.store`, `sb.store`, `store.*` (14) y `adm.store_*` (16) en ES/EN/JA/ZH/RU/KO/PT-BR/PT-PT.
  `set.always_d` ahora incluye STORE. Estilos `.store-*` en `site.css` (responsive 580px).
  Sin caché (como Noticias). Build 0 warnings.

## 2026-09-14 — se eliminan las categorías Eventos, Roblox y Vlog
- Fuera del nav (`_Layout`), de Ajustes (solo queda el toggle de Showcase) y del `HomeController`
  (acciones + helpers de scrapeo). Vistas `Eventos/Roblox/Vlog.cshtml` y modelos
  `EventosPageInfo/RobloxPageInfo/VlogPageInfo` eliminados. URLs viejas → 404.
- Ajustes limpia los claims `cat_eventos`/`cat_vlog` heredados al guardar.
- Textos `set.lead`/`set.always_d` actualizados en los 8 idiomas. Claves `ev/rx/vg/nav.*` quedan huérfanas en `i18n.js` (sin uso, sin efecto).

## 2026-09-13 — servidor local fijo sin túnel
- El servidor corre en `http://localhost:5215` (puerto fijo) vía `servidor-watchdog.bat`
  (supervisor: si el puerto está libre arranca el publicado Release, si se cae lo relanza a los 5 s;
  si el puerto está ocupado —p. ej. VS con F5— espera y toma el relevo al liberarse).
- Auto-arranque al iniciar sesión: acceso directo `InnovateStore` en Inicio de Windows (sin admin).
- `start-server.bat` / `stop-server.bat` para arrancar/detener a mano. Log en `%TEMP%\innovate-store.log`.
  `start-server.bat` también restaura el túnel público si la URL guardada no responde
  (`scripts\public-tunnel.ps1`, última URL en `%TEMP%\innovate-public-url.txt`).
- `Abrir-INNOVATE-STORE.bat` en el escritorio: doble clic → levanta servidor + túnel,
  REUTILIZA el mismo link si el túnel sigue vivo (solo cambia si el túnel murió) y abre el navegador.
- El launcher apunta SIEMPRE a `http://localhost:5215` (`api/launcher/*`): sin túnel, sin URLs cambiantes.
  Túnel (`cloudflared tunnel --url http://localhost:5215`) solo si el launcher está en otro PC/red.
