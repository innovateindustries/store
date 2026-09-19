using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Cuentas: Identity + SQLite local (app.db)
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=app.db"));
builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 6;
    o.Password.RequireDigit = false;
    o.Password.RequireLowercase = false;
    o.Password.RequireUppercase = false;
    o.Password.RequireNonAlphanumeric = false;
    o.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
    o.SlidingExpiration = true;
});

// Conexión con otras webs: HttpClientFactory para APIs externas (tienda en vivo).
// Plataforma en vivo: única fuente externa https://innovate-industries.itch.io/innovate-launcher
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("ItchIo", client =>
{
    client.BaseAddress = new Uri("https://innovate-industries.itch.io/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "INNOVATE-STORE/1.0");
});

// Eventos en vivo: única fuente externa https://webbun.github.io/innovate-industrie/
builder.Services.AddHttpClient("Eventos", client =>
{
    client.BaseAddress = new Uri("https://webbun.github.io/innovate-industrie/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "INNOVATE-STORE/1.0");
});

// Showcase en vivo: estado del directo https://www.twitch.tv/tarloox
builder.Services.AddHttpClient("TwitchStatus", client =>
{
    client.BaseAddress = new Uri("https://decapi.me/twitch/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "INNOVATE-STORE/1.0");
});

// Vlog en vivo: cuenta https://www.instagram.com/innovateindustriesof/
builder.Services.AddHttpClient("Instagram", client =>
{
    client.BaseAddress = new Uri("https://www.instagram.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
});

// Proxy inverso (Cloudflare / Azure / hosting): respeta X-Forwarded-For y X-Forwarded-Proto
// para que Request.Scheme/Host sean los públicos (Twitch parent, redirects, cookies Secure).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Panel admin estático (GitHub Pages) → API: solo la vitrina pública + JSON sin cookies.
builder.Services.AddCors(o => o.AddPolicy("Pages", p => p
    .WithOrigins("https://innovateindustries.github.io")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Crea app.db con el esquema de Identity si no existe.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Tabla de keys (para bases ya creadas antes de esta versión).
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"InviteKeys\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_InviteKeys\" PRIMARY KEY AUTOINCREMENT, " +
        "\"Code\" TEXT NOT NULL, " +
        "\"CreatedByUserId\" TEXT NULL, " +
        "\"CreatedAtUtc\" TEXT NOT NULL, " +
        "\"UsedByUserId\" TEXT NULL, " +
        "\"UsedAtUtc\" TEXT NULL, " +
        "\"IsActive\" INTEGER NOT NULL)");
    db.Database.ExecuteSqlRaw(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_InviteKeys_Code\" ON \"InviteKeys\" (\"Code\")");

    // Keys de fundador (registro con rol FOUNDER desde el launcher).
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"FounderKeys\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_FounderKeys\" PRIMARY KEY AUTOINCREMENT, " +
        "\"Code\" TEXT NOT NULL, " +
        "\"CreatedByUserId\" TEXT NULL, " +
        "\"CreatedAtUtc\" TEXT NOT NULL, " +
        "\"UsedByUserId\" TEXT NULL, " +
        "\"UsedAtUtc\" TEXT NULL, " +
        "\"IsActive\" INTEGER NOT NULL, " +
        "\"MaxUses\" INTEGER NOT NULL DEFAULT 1, " +
        "\"UsesCount\" INTEGER NOT NULL DEFAULT 0, " +
        "\"ExpiresAtUtc\" TEXT NULL)");
    db.Database.ExecuteSqlRaw(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_FounderKeys_Code\" ON \"FounderKeys\" (\"Code\")");

    // Tabla de noticias (para bases ya creadas antes de esta versión).
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"NewsItems\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_NewsItems\" PRIMARY KEY AUTOINCREMENT, " +
        "\"Title\" TEXT NOT NULL, " +
        "\"Body\" TEXT NOT NULL, " +
        "\"ImagePath\" TEXT NULL, " +
        "\"LinkUrl\" TEXT NULL, " +
        "\"LinkText\" TEXT NULL, " +
        "\"IsPublished\" INTEGER NOT NULL, " +
        "\"CreatedAtUtc\" TEXT NOT NULL, " +
        "\"CreatedByUserId\" TEXT NULL)");

    // Tablas del launcher: sesiones de juego y tokens de API.
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"PlaySessions\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_PlaySessions\" PRIMARY KEY AUTOINCREMENT, " +
        "\"UserId\" TEXT NOT NULL, " +
        "\"GameSlug\" TEXT NOT NULL, " +
        "\"GameTitle\" TEXT NOT NULL, " +
        "\"StartedAtUtc\" TEXT NOT NULL, " +
        "\"EndedAtUtc\" TEXT NULL, " +
        "\"Seconds\" INTEGER NOT NULL)");
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"LauncherTokens\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_LauncherTokens\" PRIMARY KEY AUTOINCREMENT, " +
        "\"UserId\" TEXT NOT NULL, " +
        "\"TokenHash\" TEXT NOT NULL, " +
        "\"CreatedAtUtc\" TEXT NOT NULL, " +
        "\"ExpiresAtUtc\" TEXT NOT NULL, " +
        "\"Revoked\" INTEGER NOT NULL)");
    db.Database.ExecuteSqlRaw(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_LauncherTokens_TokenHash\" ON \"LauncherTokens\" (\"TokenHash\")");

    // Juegos de la STORE (kit de publicación del Admin).
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"StoreGames\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_StoreGames\" PRIMARY KEY AUTOINCREMENT, " +
        "\"Title\" TEXT NOT NULL, " +
        "\"Description\" TEXT NOT NULL, " +
        "\"MinRequirements\" TEXT NOT NULL, " +
        "\"RecRequirements\" TEXT NOT NULL, " +
        "\"IsPreorder\" INTEGER NOT NULL, " +
        "\"IsEarlyAccess\" INTEGER NOT NULL, " +
        "\"Price\" TEXT NOT NULL, " +
        "\"CoverPath\" TEXT NOT NULL, " +
        "\"ScreenshotsJson\" TEXT NOT NULL, " +
        "\"TrailerUrl\" TEXT NOT NULL, " +
        "\"IsPublished\" INTEGER NOT NULL, " +
        "\"CreatedAtUtc\" TEXT NOT NULL, " +
        "\"CreatedByUserId\" TEXT NULL)");
    // Pedidos de pago manual de la STORE.
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"StoreOrders\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_StoreOrders\" PRIMARY KEY AUTOINCREMENT, " +
        "\"GameId\" INTEGER NOT NULL, " +
        "\"GameTitle\" TEXT NOT NULL, " +
        "\"Price\" TEXT NOT NULL, " +
        "\"Reference\" TEXT NOT NULL, " +
        "\"Status\" TEXT NOT NULL, " +
        "\"CreatedAtUtc\" TEXT NOT NULL, " +
        "\"BuyerUserId\" TEXT NULL)");
    db.Database.ExecuteSqlRaw(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_StoreOrders_Reference\" ON \"StoreOrders\" (\"Reference\")");
    // Key del juego: se genera al marcar Pagado (para bases ya creadas).
    var orderCols = db.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('StoreOrders')").ToList();
    if (!orderCols.Contains("KeyCode"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"StoreOrders\" ADD COLUMN \"KeyCode\" TEXT NULL");
    // Canje único en el launcher: quién/cuándo canjeó la key (para bases ya creadas).
    if (!orderCols.Contains("RedeemedByUserId"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"StoreOrders\" ADD COLUMN \"RedeemedByUserId\" TEXT NULL");
    if (!orderCols.Contains("RedeemedAtUtc"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"StoreOrders\" ADD COLUMN \"RedeemedAtUtc\" TEXT NULL");
    db.Database.ExecuteSqlRaw(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_StoreOrders_KeyCode\" ON \"StoreOrders\" (\"KeyCode\")");
    // Builds del launcher (descargables desde Plataforma).
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"LauncherBuilds\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_LauncherBuilds\" PRIMARY KEY AUTOINCREMENT, " +
        "\"Version\" TEXT NOT NULL, " +
        "\"Notes\" TEXT NULL, " +
        "\"FilePath\" TEXT NOT NULL, " +
        "\"FileName\" TEXT NOT NULL, " +
        "\"SizeBytes\" INTEGER NOT NULL, " +
        "\"IsPublished\" INTEGER NOT NULL, " +
        "\"Downloads\" INTEGER NOT NULL, " +
        "\"CreatedAtUtc\" TEXT NOT NULL, " +
        "\"CreatedByUserId\" TEXT NULL)");
    // Columnas añadidas después (para bases ya creadas).
    var buildCols = db.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('LauncherBuilds')").ToList();
    if (!buildCols.Contains("Description"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"LauncherBuilds\" ADD COLUMN \"Description\" TEXT NULL");
    if (!buildCols.Contains("Specs"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"LauncherBuilds\" ADD COLUMN \"Specs\" TEXT NULL");
    if (!buildCols.Contains("ScreenshotPath"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"LauncherBuilds\" ADD COLUMN \"ScreenshotPath\" TEXT NULL");
    if (!buildCols.Contains("IsMandatory"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"LauncherBuilds\" ADD COLUMN \"IsMandatory\" INTEGER NOT NULL DEFAULT 0");

    // Puntos canjeables del launcher (1 USD = 100 pts).
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"LauncherPoints\" (" +
        "\"UserId\" TEXT NOT NULL CONSTRAINT \"PK_LauncherPoints\" PRIMARY KEY, " +
        "\"Points\" INTEGER NOT NULL DEFAULT 0, " +
        "\"UpdatedAtUtc\" TEXT NOT NULL)");
    // Foto de perfil del launcher.
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"LauncherProfiles\" (" +
        "\"UserId\" TEXT NOT NULL CONSTRAINT \"PK_LauncherProfiles\" PRIMARY KEY, " +
        "\"ImagePath\" TEXT NOT NULL DEFAULT '', " +
        "\"UpdatedAtUtc\" TEXT NOT NULL)");
    // Builds distribuibles por juego (manifiestos del launcher).
    db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS \"GameBuilds\" (" +
        "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameBuilds\" PRIMARY KEY AUTOINCREMENT, " +
        "\"GameId\" INTEGER NOT NULL, " +
        "\"Version\" TEXT NOT NULL, " +
        "\"Exe\" TEXT NOT NULL DEFAULT '', " +
        "\"ManifestJson\" TEXT NOT NULL DEFAULT '', " +
        "\"TotalSize\" INTEGER NOT NULL DEFAULT 0, " +
        "\"CreatedAtUtc\" TEXT NOT NULL, " +
        "\"CreatedByUserId\" TEXT NULL)");
    db.Database.ExecuteSqlRaw(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_GameBuilds_GameId\" ON \"GameBuilds\" (\"GameId\")");
    // Ficha extendida de la STORE (para bases ya creadas).
    var gameCols = db.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('StoreGames')").ToList();
    if (!gameCols.Contains("Genre"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"StoreGames\" ADD COLUMN \"Genre\" TEXT NOT NULL DEFAULT ''");
    if (!gameCols.Contains("Regions"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"StoreGames\" ADD COLUMN \"Regions\" TEXT NOT NULL DEFAULT ''");
    if (!gameCols.Contains("InPass"))
        db.Database.ExecuteSqlRaw("ALTER TABLE \"StoreGames\" ADD COLUMN \"InPass\" INTEGER NOT NULL DEFAULT 0");

    // Roles del sistema.
    var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var r in new[] { "CEO", "FOUNDER", "INTERNO", "PUBLISHER" })
        if (!await roles.RoleExistsAsync(r))
            await roles.CreateAsync(new IdentityRole(r));
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors("Pages");

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
