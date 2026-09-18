using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Data
{
    // Base de datos de cuentas (Identity). SQLite local: app.db
    public class AppDbContext : IdentityDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<InviteKey> InviteKeys => Set<InviteKey>();

        public DbSet<NewsItem> NewsItems => Set<NewsItem>();

        public DbSet<PlaySession> PlaySessions => Set<PlaySession>();

        public DbSet<LauncherToken> LauncherTokens => Set<LauncherToken>();

        public DbSet<LauncherBuild> LauncherBuilds => Set<LauncherBuild>();

        public DbSet<StoreGame> StoreGames => Set<StoreGame>();

        public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
    }
}
