---
description: Build + publish .NET y deploy de portada a Cloudflare Pages.
agent: build
---

Compila y publica la tienda y sube la portada estática:

1. `dotnet build` — debe quedar sin warnings nuevos.
2. `dotnet publish -c Release` — verifica que `Publish/index.html` se copió por el target MSBuild.
3. `npx wrangler pages deploy ./Publish --project-name innovate-store --branch main $ARGUMENTS`
4. Recordar reglas `.agent/CLOUDFLARE-WEB-TIENDA-LAUNCHER.md`: no cachear `/Account/*`, `/Admin/*`, `/api/launcher/*`.
