# WORKFLOW optimizado — INNOVATE STORE

## Arranque sesión (2 min)
1. Leer `CHANGELOG.md` + `.agent/README.md`.
2. `dotnet build` (cero warnings nuevos). `npx wrangler whoami` si toca edge.
3. Abrir `Views/Shared/_Layout.cshtml` si toca UI; `Program.cs` si toca servicios/caché.

## Hacer cambios
- UI: `_Layout` → `site.css` → `site.js` → vista → `i18n.js` (8 idiomas) → captura desktop+móvil.
- Backend: `Program.cs` (servicios/caché) → `Controllers/` → `Data/AppDbContext.cs` (+ SQL `IF NOT EXISTS` si nueva tabla, como `InviteKeys`/`NewsItems`/`PlaySessions`) → `dotnet build`.
- Noticias/Admin: respetar roles, `INN-XXXXXXXX`, ≤5 MB, borrar archivo al eliminar.
- Externos: no cambiar URLs base sin actualizar caché (5/5/2/10 min) y límites (itch sin iframe, IG `/embed`, Twitch `Request.Host`).

## Publicar
```bash
dotnet build
dotnet publish -c Release
npx wrangler pages deploy ./Publish --project-name innovate-store --branch main
```
Cloudflare: aplicar reglas `CLOUDFLARE-*.md` (no cachear `/Account/*`, `/Admin/*`, `/api/launcher/*`).

## Definición de listo
- [ ] Build OK, i18n 8 idiomas, responsive 900/580, sin FOUC, sin CLS
- [ ] Roles/keys/noticias probados (incluye no-auto-baneo)
- [ ] `api/launcher` probado vía Tunnel si cambió
- [ ] CHANGELOG actualizado, sin `app.db*`/secretos en git
