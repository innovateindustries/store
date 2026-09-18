# AGENTS.md — INNOVATE INDUSTRIES WEB STORE

> Instrucciones para agentes (opencode / Claude Code / Codex / Cursor). Fuente de verdad: `CHANGELOG.md` + `.agent/`.

## Proyecto
- ASP.NET Core MVC · .NET 10 · SQLite (`app.db`, `EnsureCreated`, tablas extra por `ExecuteSqlRaw` en `Program.cs:70-121`)
- Identity: registro, login email-o-usuario, logout, roles `CEO, FOUNDER, INTERNO, PUBLISHER` (`Program.cs:12-30,123-127`)
- Tema neón gamer global: fondo 3D Three.js + animaciones GSAP. No romper: `Views/Shared/_Layout.cshtml`, `wwwroot/css/site.css`, `wwwroot/js/site.js`, `wwwroot/js/i18n.js`
- 8 idiomas con banderas (ES, EN, JA, ZH, RU, KO, PT-BR, PT-PT), persistencia navegador. Toda vista nueva debe llevar claves `data-i18n`.
- `app.UseStaticFiles()` explícito es requerido en .NET 10 para `wwwroot/uploads/news/` (`Program.cs:139`)
- `Publish/index.html` se copia a publish vía target MSBuild (`INNOVATE INDUSTRIES WEB STORE.csproj:17-20`)
- `.gitignore`: `bin/`, `obj/`, `.vs/`, `*.user`, `*.db*`, secretos. Nunca commitear `app.db*` ni credenciales fundador.

## Estructura viva
- `Controllers/` + `Controllers/Api/LauncherApiController.cs` (`api/launcher`: register, login, ping, me, heartbeat, session/end)
- `Models/LauncherModels.cs`, `Data/AppDbContext.cs` (más `InviteKeys`, `NewsItems`, `PlaySessions`, `LauncherTokens` creadas por SQL crudo)
- `Views/Home/`: Index, Plataforma, Eventos, Roblox, Showcase, Vlog, Noticias
- `Views/Admin/`: Index, Noticias, Crear, Horas.cshtml
- `Views/Account/`: Login, Register, Plans, Ajustes, InternalKey, InternalRegister
- Cachés: itch.io 5 min, eventos 5 min, Twitch estado 2 min, Instagram live 10 min. Noticias: sin caché.

## Reglas de trabajo
1. Leer `CHANGELOG.md` antes de tocar nav, roles, o integraciones externas.
2. Twitch embed: `parent` = solo `Request.Host` (localhost en dev, dominio en prod). Instagram: usar `/embed`, nunca iframe de perfil. itch.io: solo metadatos, prohíbe iframes.
3. Admin: ban/desban solo CEO/FOUNDER, nunca auto-baneo. Keys `INN-XXXXXXXX` un solo uso, contraseña = key.
4. Noticias: imagen JPG/PNG/GIF/WEBP ≤5 MB en `wwwroot/uploads/news/`, borrar archivo al eliminar.
5. Cambios .cshtml: mantener responsive + neón + i18n. Cambios .cs: `dotnet build`, no dejar warnings nuevos.
6. Cloudflare está DELANTE del hosting (no reescribir a Workers). Ver `.agent/CLOUDFLARE-WEB-TIENDA-LAUNCHER.md`.
7. Web moderna: ver `.agent/HTML-WEB-MODERNA.md` (GSAP `set` antes de `to` para evitar FOUC, breakpoints, accesibilidad).
8. Flujo: ver `.agent/WORKFLOW.md`. Skills/repos curados: `.agent/SKILLS-REPOS.md`.

## Comandos
- `dotnet build` / `dotnet run` / `dotnet publish`
- `npx wrangler whoami` / `npx wrangler pages deploy ./Publish --project-name innovate-store`
- `npx skills add https://github.com/cloudflare/skills`
