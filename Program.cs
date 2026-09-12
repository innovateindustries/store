using INNOVATE_INDUSTRIES_WEB_STORE.Data;
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
