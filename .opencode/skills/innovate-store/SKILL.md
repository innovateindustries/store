---
name: innovate-store
description: Use when working on INNOVATE INDUSTRIES WEB STORE ASP.NET MVC shop, launcher api/launcher, Cloudflare front, or neon Three.js GSAP theme. Covers roles, news, i18n 8 langs, cache rules.
---

# Innovate Store — skill de proyecto

Trabaja sobre `AGENTS.md` + `CHANGELOG.md` + `.agent/`.

- Web: Razor + Bootstrap + `site.css` + Three.js + GSAP. `gsap.set` antes de `to`. Breakpoints 900/580.
- i18n ES EN JA ZH RU KO PT-BR PT-PT con `data-i18n` en toda vista nueva.
- Cachés: itch 5min, eventos 5min, Twitch 2min, IG 10min, noticias sin caché.
- Roles CEO FOUNDER INTERNO PUBLISHER. Keys `INN-XXXXXXXX`.
- Cloudflare: Tunnel dev, Pages para `Publish/index.html`, R2 futuro para `uploads/news`, WAF + no-cache en `/Account/*`, `/Admin/*`, `/api/launcher/*`.
- Launcher: `register, login, ping, me, heartbeat, session/end`. Versión mínima en `ping`.
- Cierre: `dotnet build` + captura desktop/móvil + CHANGELOG.
