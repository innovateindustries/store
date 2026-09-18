# HTML / Web moderna — reglas del tema neón INNOVATE

Stack real: Razor `.cshtml` + Bootstrap + `site.css` + `site.js` + `i18n.js` + Three.js fondo + GSAP. No migrar a Next.js; robarle los patrones.

## Patrones obligatorios (de skills `award-craft`, `awwwards-3d`, `landing-craft`, `ship-page-skill`)
1. **Sin FOUC:** todo elemento animado lleva `gsap.set(..., {opacity:0, y:30})` ANTES de cualquier `timeline`/`to`. Solo GSAP por CDN + fuentes Google como externos.
2. **Hero 100vh:** eyebrow + H1 68-82px + subtítulo + CTA `.btn-primary` + indicador scroll. Capas parallax `back/mid/content` con mousemove.
3. **Scroll reveals:** `ScrollTrigger.batch(".feature-card", {start:"top 80%"})` con `rotateX:18 → 0`, `stagger:0.11`, `power2.out`.
4. **Breakpoints:** 900px (3→2 col) y 580px (2→1 col). Mobile-first, `loading="lazy"` en imágenes, sin `#fff` plano (usar neutros cálidos / oscuros neón).
5. **Accesibilidad:** jerarquía h1→h2→h3 sin saltos, `alt` en toda imagen de noticia, `focus-visible`, contraste AA, `prefers-reduced-motion` desactiva Three.js/GSAP.
6. **i18n:** toda cadena nueva en `i18n.js` ES/EN/JA/ZH/RU/KO/PT-BR/PT-PT con `data-i18n`. Nunca texto duro solo en español.
7. **Performance:** Three.js un solo canvas fijo detrás (`position:fixed`, `z-index:-1`), `EffectComposer` solo si hace falta; Lenis para scroll suave opcional; imágenes news ≤5 MB, preferir WebP.

## Dónde tocar
- Layout: `Views/Shared/_Layout.cshtml` (fondo, nav 8 categorías, footer)
- Estilos: `wwwroot/css/site.css` (tokens neón, `.btn-primary`, `.feature-card`, `.eyebrow`)
- JS: `wwwroot/js/site.js` (GSAP/Three init), `wwwroot/js/i18n.js` (diccionarios)
- Secciones: `Views/Home/Index.cshtml` (hero + últimas 3 noticias), resto `Views/Home/*.cshtml`

## Verificación visual (no basta `dotnet build`)
1. `dotnet run` → captura desktop + móvil de la vista tocada
2. Revisar: sin FOUC, reveals disparan a 80%, sin CLS, contraste OK, 8 idiomas cambian la vista
3. Si falla: fix → re-captura (máx 3 iteraciones por issue)

## Anti-patrones
- Hardcodear `parent` Twitch, iframe de perfil Instagram, iframe itch.io
- CSS/JS externos salvo CDN permitidos; `style=""` inline prohibido (usar clases)
- Más de 6 features por grid, lorem ipsum, rutas absolutas `C:\` en vistas
