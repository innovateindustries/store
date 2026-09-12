# INNOVATE INDUSTRIES WEB STORE — Bitácora de cambios

Documento vivo de la evolución del proyecto. Última actualización: checkpoint actual.

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
| Plataforma | https://innovate-industries.itch.io/innovate-launcher (metadatos + estado, caché 5 min) |
| Eventos | https://webbun.github.io/innovate-industrie/ (eventos + cuenta regresiva + launcher, caché 5 min) |
| Roblox | Hub local (pendiente link oficial para sync por Roblox Games API) |
| Showcase | https://www.twitch.tv/tarloox (player + chat embebidos, estado cada 2 min) |
| Vlog | https://www.instagram.com/innovateindustriesof/ (widget embebido + verificación live cada 10 min) |
| Noticias | Tabla local `NewsItems` (publicadas desde Admin, sin caché) |
| Ajustes | Visibilidad por usuario: Showcase/Eventos/Vlog desactivadas por defecto |

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
