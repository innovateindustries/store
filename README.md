# INNOVATE INDUSTRIES WEB STORE

Tienda + launcher ASP.NET Core MVC (.NET 10).

- Web interactiva: `dotnet run` → https://localhost:7009 (requiere .NET 10 + `app.db` que se crea sola).
- Vitrina estática gratuita (GitHub Pages): https://innovateindustries.github.io/store/
  (snapshot real en `docs/`: Inicio, STORE, Plataforma, Showcase, Noticias...; regenerar con
  `powershell -ExecutionPolicy Bypass -File scripts/snapshot-pages.ps1` con el servidor en
  `http://localhost:5215`. Sin login/compras/admin: solo vitrina).
- Repo: https://github.com/innovateindustries/store