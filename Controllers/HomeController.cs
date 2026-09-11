using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Filters;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _db;

        public HomeController(IHttpClientFactory httpClientFactory, IMemoryCache cache, AppDbContext db)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _db = db;
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

        public IActionResult Privacy()
        {
            return View();
        }

        // NUESTRA PLATAFORMA en vivo: sincroniza metadatos de
        // https://innovate-industries.itch.io/innovate-launcher (única fuente externa).
        // Se cachea 5 minutos para no saturar a itch.io.
        public async Task<IActionResult> Plataforma()
        {
            const string cacheKey = "itch:innovate-launcher";
            if (!_cache.TryGetValue(cacheKey, out ItchPageInfo? info) || info is null)
            {
                info = await FetchItchPageAsync();
                _cache.Set(cacheKey, info, TimeSpan.FromMinutes(5));
            }
            return View(info);
        }

        private async Task<ItchPageInfo> FetchItchPageAsync()
        {
            var info = new ItchPageInfo { FetchedAtUtc = DateTime.UtcNow };
            try
            {
                var client = _httpClientFactory.CreateClient("ItchIo");
                using var response = await client.GetAsync("innovate-launcher", HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                info.Success = true;
                info.Title = ExtractMeta(html, "og:title") ?? CleanTitle(ExtractTitleTag(html)) ?? info.Title;
                info.Description = ExtractMeta(html, "og:description") ?? string.Empty;
                info.ImageUrl = ExtractMeta(html, "og:image");
                info.IsPasswordProtected = html.Contains("password is required", StringComparison.OrdinalIgnoreCase)
                    || html.Contains("game_password", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                info.Success = false;
                info.ErrorMessage = ex.Message;
            }
            return info;
        }

        private static string? ExtractMeta(string html, string property)
        {
            var m = Regex.Match(html, $"<meta[^>]+property=\"{property}\"[^>]+content=\"([^\"]*)\"",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!m.Success)
                m = Regex.Match(html, $"<meta[^>]+content=\"([^\"]*)\"[^>]+property=\"{property}\"",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
        }

        private static string? ExtractTitleTag(string html)
        {
            var m = Regex.Match(html, "<title>(.*?)</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim()) : null;
        }

        private static string? CleanTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;
            const string suffix = " - itch.io";
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                title = title[..^suffix.Length].Trim();
            return title;
        }

        // EVENTOS en vivo: sincroniza https://webbun.github.io/innovate-industrie/ (única fuente).
        // Se cachea 5 minutos para no saturar la web de eventos.
        [CategoryEnabled("eventos")]
        public async Task<IActionResult> Eventos()
        {
            const string cacheKey = "eventos:innovate-industrie";
            if (!_cache.TryGetValue(cacheKey, out EventosPageInfo? info) || info is null)
            {
                info = await FetchEventosPageAsync();
                _cache.Set(cacheKey, info, TimeSpan.FromMinutes(5));
            }
            return View(info);
        }

        private async Task<EventosPageInfo> FetchEventosPageAsync()
        {
            var info = new EventosPageInfo { FetchedAtUtc = DateTime.UtcNow };
            try
            {
                var client = _httpClientFactory.CreateClient("Eventos");
                var html = await client.GetStringAsync("", HttpContext.RequestAborted);

                info.Success = true;
                info.Title = ExtractTitleTag(html) ?? info.Title;

                var eventosSection = Regex.Match(html, "<section id=\"eventos\".*?</section>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase).Value;
                if (!string.IsNullOrEmpty(eventosSection))
                {
                    info.SectionTitle = StripTags(FirstGroup(eventosSection, "<h2[^>]*>(.*?)</h2>"));
                    info.SectionDesc = StripTags(FirstGroup(eventosSection, "<p class=\"desc\"[^>]*>(.*?)</p>"));

                    foreach (var row in Regex.Split(eventosSection, "<div class=\"event-row\">").Skip(1))
                    {
                        var name = StripTags(FirstGroup(row, "<h3[^>]*>(.*?)</h3>"));
                        if (string.IsNullOrWhiteSpace(name))
                            continue;
                        info.Events.Add(new EventoInfo
                        {
                            Tag = StripTags(FirstGroup(row, "<span class=\"tag[^\"]*\"[^>]*>(.*?)</span>")),
                            Name = name,
                            Description = StripTags(FirstGroup(row, "<p[^>]*>(.*?)</p>")),
                            HasCountdown = row.Contains("uhc-days", StringComparison.OrdinalIgnoreCase),
                            StatusText = StripTags(FirstGroup(row, "<span class=\"reveal-tag\"[^>]*>(.*?)</span>"))
                        });
                    }
                }

                var dateMatch = Regex.Match(html, @"new Date\('([^']+)'\)");
                if (dateMatch.Success && DateTimeOffset.TryParse(dateMatch.Groups[1].Value, out var target))
                    info.NextEventDate = target;

                var launcherSection = Regex.Match(html, "<section id=\"launcher\".*?</section>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase).Value;
                if (!string.IsNullOrEmpty(launcherSection))
                {
                    info.LauncherVersion = StripTags(FirstGroup(launcherSection, "<span class=\"tag[^\"]*\"[^>]*>(v[\\d.]+[^<]*)</span>"));
                    info.LauncherName = StripTags(FirstGroup(launcherSection, "<h3[^>]*>(.*?)</h3>"));
                    info.LauncherDesc = StripTags(FirstGroup(launcherSection, "<p[^>]*>(.*?)</p>"));
                }
            }
            catch (Exception ex)
            {
                info.Success = false;
                info.ErrorMessage = ex.Message;
            }
            return info;
        }

        private static string FirstGroup(string input, string pattern)
        {
            var m = Regex.Match(input, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim()) : string.Empty;
        }

        private static string StripTags(string? s) =>
            string.IsNullOrEmpty(s)
                ? string.Empty
                : System.Net.WebUtility.HtmlDecode(Regex.Replace(s, "<[^>]+>", string.Empty).Trim());

        // ROBLOX: hub de la categoría. Aún sin link oficial: cuando se reciba
        // el juego/grupo se conectará el sync en vivo (Roblox Games API).
        public IActionResult Roblox()
        {
            return View(new RobloxPageInfo());
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

        // VLOG en vivo: https://www.instagram.com/innovateindustriesof/
        // Instagram solo entrega un cascarón JS a bots (sin bio/posts/datos) y
        // bloquea iframes del perfil, así que se verifica la cuenta en vivo y
        // los reels/posts se embeben con el player oficial al recibir sus links.
        [CategoryEnabled("vlog")]
        public async Task<IActionResult> Vlog()
        {
            const string cacheKey = "ig:innovateindustriesof:status";
            if (!_cache.TryGetValue(cacheKey, out VlogPageInfo? info) || info is null)
            {
                info = await FetchInstagramStatusAsync();
                _cache.Set(cacheKey, info, TimeSpan.FromMinutes(10));
            }
            return View(info);
        }

        private async Task<VlogPageInfo> FetchInstagramStatusAsync()
        {
            var info = new VlogPageInfo { FetchedAtUtc = DateTime.UtcNow };
            try
            {
                var client = _httpClientFactory.CreateClient("Instagram");
                using var req = new HttpRequestMessage(HttpMethod.Head, "innovateindustriesof/");
                using var res = await client.SendAsync(req, HttpContext.RequestAborted);
                info.Success = true;
                info.IsReachable = res.IsSuccessStatusCode;
            }
            catch
            {
                info.Success = false;
                info.IsReachable = false;
            }
            return info;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
