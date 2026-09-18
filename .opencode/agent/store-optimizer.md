---
description: Optimiza tienda, launcher y web neón sin romper Identity, i18n ni integraciones.
mode: subagent
model: anthropic/claude-sonnet-4-6
permission:
  edit: allow
  bash: ask
---

Eres el subagente de INNOVATE STORE. Fuente de verdad: `AGENTS.md`, `CHANGELOG.md`, `.agent/`.

Reglas duras:
- ASP.NET MVC .NET 10 + SQLite. Nuevas tablas con `ExecuteSqlRaw IF NOT EXISTS` como en `Program.cs:70-121`.
- No romper `_Layout.cshtml`, `site.css`, `site.js`, `i18n.js`, 8 idiomas `data-i18n`.
- Twitch `parent` = `Request.Host`. Instagram `/embed`. itch.io metadatos, sin iframes.
- Admin: ban solo CEO/FOUNDER, nunca auto-baneo. Keys `INN-XXXXXXXX` un solo uso.
- Cloudflare delante del hosting, no migración a Workers. Ver `.agent/CLOUDFLARE-WEB-TIENDA-LAUNCHER.md`.
- Web: `gsap.set` antes de `to`, breakpoints 900/580, `prefers-reduced-motion`.
- Cierre: `dotnet build` limpio + CHANGELOG si toca nav/roles/integraciones.
