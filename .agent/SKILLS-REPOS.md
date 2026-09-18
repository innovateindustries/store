# Skills y repos curados — Cloudflare + tienda + launcher + web moderna

## Instalar primero (oficial Cloudflare, compatible opencode)
```bash
npx skills add https://github.com/cloudflare/skills
```
Cubre: `cloudflare`, `wrangler`, `web-perf`, `turnstile-spin`, `durable-objects`, `agents-sdk`, `sandbox-*`, `workers-best-practices`, `cloudflare-one`, `nextjs-on-cloudflare`.
Docs setup opencode: https://developers.cloudflare.com/agent-setup/opencode/
MCP ya cableado en `opencode.json`: `cloudflare`, `cloudflare-docs`, `cloudflare-bindings`, `cloudflare-builds`, `cloudflare-observability`.

| Skill oficial | Para qué en ESTE proyecto |
|---|---|
| `cloudflare` | elegir producto, arquitectura edge-delante-de-ASP.NET |
| `wrangler` | Pages (`Publish/index.html`), Tunnel localhost, R2, tail |
| `web-perf` | auditar LCP/CLS/INP del Layout neón |
| `turnstile-spin` | captcha en Login/Register sin romper Identity |
| `cloudflare-one` | Tunnel + Access para panel Admin / launcher dev |

## Repos tienda (ideas, no migraciones)
- `aaliyaan/minshop` — tienda Astro + Workers D1+R2, admin + Stripe + MCP + Agent API (`GET /api/products`, `POST /api/checkout`). Robar: `/llms.txt` y API legible por agentes.
- `rahmatullahboss/dc-store` — template Next.js 16 + D1/R2 + Better Auth + carrito localStorage. Robar: flujo catálogo→carrito→pedido, dashboard admin.
- `ToKnow-ai/cloudflare-nextjs-skill` — skill que arma shop+blog+admin a $0 con plan por fases. Robar: fast-path 3 preguntas + `IMPLEMENTATION.md` por fases.
- `cloudflare/templates/commerce-llms-txt-template` — `/llms.txt` + `/llms-full.txt` con Workers AI + KV para que ChatGPT/Perplexity recomienden tu catálogo.
- `haoyiyin/instant-site` — `SKILL.md` + `DESIGN.md` + SEO 3 capas + multi-idioma + `state.json`. Robar: auditoría SEO y registry multi-sitio.

## Deploy Pages
- `Wievondii/cf-pages-deploy` — skill deploy Pages + dominio + DNS + troubleshooting. Windows: copiar `SKILL.md` a `%USERPROFILE%\.config\opencode\skills\cf-pages-deploy\SKILL.md`.
- `openai/skills/.curated/cloudflare-deploy` — árboles de decisión Workers vs Pages vs Containers vs Cron.

## Web moderna / landing neón (tu tema ya es Three.js + GSAP)
- `alanvaa06/award-craft` — 6 skills, gate plan→build→assets→verify con screenshot obligatorio. Exige `gsap-skills` + `impeccable v4`.
- `tsogjavklann/awwwards-3d` — Three.js r170 + GSAP 3.12.5 + Lenis 1.1, 4 templates (`minimal`, `coin-scroll`, `room-walkthrough`, `glass-product`), 33 snippets, 20 anti-patrones.
- `can4hou6joeng4/landing-craft` — 57 sistemas de diseño por ciudad, GSAP ScrollTrigger, clip-paths, preview interactivo, deploy 1-click.
- `zooeyii/ship-page-skill` — landing en 1 HTML cero-deps, 7 presets, IntersectionObserver + counters + partículas. Ideal para `Publish/index.html`.
- `kash-123/cinematic-scroll-landing` — scroll cinematográfico, libro que pasa páginas con scroll, harness puppeteer + Lighthouse 100/0 CLS.
- `alirezarezvani/claude-skills` (`cs-landing`) + `sinhoneyy-landing` — landing 1-archivo con validador (`gsap.set` antes de `to`, breakpoints 900/580, solo CDN Inter+GSAP).

## Launcher (juegos)
- Tu API `api/launcher` ya cubre register/login/ping/me/heartbeat/session/end. No hay skill oficial; patrón: Squirrel.Windows / Velopack (auto-update `.nupkg` en R2) + `ping` con versión mínima → redirigir a `/Plataforma` si es viejo. Buscar en GitHub `Velopack sample WPF auto-update` al implementar cliente.
