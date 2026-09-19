using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Filters;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public HomeController(IHttpClientFactory httpClientFactory, IMemoryCache cache, AppDbContext db, IWebHostEnvironment env)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _db = db;
            _env = env;
        }

        public IActionResult Index()
        {
            ViewBag.LatestNews = _db.NewsItems
                .Where(n => n.IsPublished)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(3)
                .ToList();
            return View();
        }

        // NOTICIAS para usuarios: lee en vivo las publicadas desde el admin.
        public IActionResult Noticias()
        {
            var items = _db.NewsItems
                .Where(n => n.IsPublished)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(30)
                .ToList();
            return View(items);
        }

        // STORE: juegos publicados desde el Admin (carátula + precio + badge).
        public IActionResult Store()
        {
            var games = _db.StoreGames
                .Where(g => g.IsPublished)
                .OrderByDescending(g => g.Id)
                .Take(60)
                .ToList();
            return View(games);
        }

        // Ficha del juego: portada, descripción, requisitos, screenshots y tráiler.
        public IActionResult StoreDetalle(int id)
        {
            var game = _db.StoreGames
                .FirstOrDefault(g => g.Id == id && g.IsPublished);
            if (game == null)
                return RedirectToAction(nameof(Store));
            return View(game);
        }

        // PAGAR (no "comprar"): resumen del pedido. Requiere login.
        // Sin pasarela conectada: el pago es manual y queda Pendiente.
        // Los juegos en desarrollo (preventa) no se pueden comprar aún.
        [Authorize]
        public IActionResult Pagar(int id)
        {
            var game = _db.StoreGames
                .FirstOrDefault(g => g.Id == id && g.IsPublished);
            if (game == null || game.IsPreorder)
                return RedirectToAction(nameof(StoreDetalle), new { id });
            return View(game);
        }

        // Confirma el pago manual: crea el pedido Pendiente con referencia.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPago(int id)
        {
            var game = _db.StoreGames
                .FirstOrDefault(g => g.Id == id && g.IsPublished);
            if (game == null || game.IsPreorder)
                return RedirectToAction(nameof(Store));

            string reference;
            do
            {
                reference = "ORD-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            } while (_db.StoreOrders.Any(o => o.Reference == reference));

            var order = new StoreOrder
            {
                GameId = game.Id,
                GameTitle = game.Title,
                Price = game.Price,
                Reference = reference,
                Status = "Pendiente",
                CreatedAtUtc = DateTime.UtcNow,
                BuyerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };
            _db.StoreOrders.Add(order);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(PagarOk), new { id = order.Id });
        }

        // Confirmación del pedido con su referencia de pago manual.
        [Authorize]
        public IActionResult PagarOk(int id)
        {
            var order = _db.StoreOrders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return RedirectToAction(nameof(Store));
            var me = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staff = User.IsInRole("CEO") || User.IsInRole("FOUNDER") || User.IsInRole("INTERNO");
            if (!staff && order.BuyerUserId != me)
                return RedirectToAction(nameof(Store));
            ViewData["Game"] = _db.StoreGames.FirstOrDefault(g => g.Id == order.GameId);
            return View(order);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // PLATAFORMA: descarga del último build publicado desde el Admin (LAUNCHER).
        public IActionResult Plataforma()
        {
            LauncherBuild? build = _db.LauncherBuilds
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.Id)
                .FirstOrDefault();
            return View(build);
        }

        // Descarga el archivo y cuenta (con soporte de rangos para archivos grandes).
        public async Task<IActionResult> DescargarLauncher()
        {
            var build = _db.LauncherBuilds
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.Id)
                .FirstOrDefault();
            if (build == null)
                return RedirectToAction(nameof(Plataforma));
            var full = Path.Combine(_env.WebRootPath, build.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(full))
                return NotFound();
            build.Downloads++;
            await _db.SaveChangesAsync();
            return PhysicalFile(full, "application/octet-stream", build.FileName, enableRangeProcessing: true);
        }

        // SHOWCASE en vivo: directo de Twitch https://www.twitch.tv/tarloox
        // El player oficial muestra el directo solo cuando hay live.
        // El estado (EN VIVO/OFFLINE) se consulta a la API pública con caché de 2 min.
        [CategoryEnabled("showcase")]
        public async Task<IActionResult> Showcase()
        {
            var info = new ShowcasePageInfo
            {
                ParentHost = HttpContext.Request.Host.Host,
                FetchedAtUtc = DateTime.UtcNow
            };

            const string cacheKey = "twitch:tarloox:status";
            if (!_cache.TryGetValue(cacheKey, out (bool live, string uptime)? status))
            {
                status = await FetchTwitchStatusAsync();
                if (status.HasValue)
                    _cache.Set(cacheKey, status.Value, TimeSpan.FromMinutes(2));
            }

            if (status.HasValue)
            {
                info.Success = true;
                info.IsLive = status.Value.live;
                info.UptimeText = status.Value.uptime;
            }
            return View(info);
        }

        private async Task<(bool live, string uptime)?> FetchTwitchStatusAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("TwitchStatus");
                var raw = (await client.GetStringAsync("uptime/tarloox", HttpContext.RequestAborted)).Trim();
                if (raw.Length == 0)
                    return null;
                if (raw.Contains("offline", StringComparison.OrdinalIgnoreCase))
                    return (false, string.Empty);
                return (true, raw.Length > 80 ? raw[..80] : raw);
            }
            catch
            {
                return null;
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
