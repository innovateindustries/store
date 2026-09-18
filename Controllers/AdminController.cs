using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text.Json;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Controllers
{
    // Subpágina de administración para el equipo interno.
    [Authorize(Roles = "CEO,FOUNDER,INTERNO")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public AdminController(UserManager<IdentityUser> users, AppDbContext db, IWebHostEnvironment env)
        {
            _users = users;
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new AdminIndexViewModel
            {
                CanManageKeys = User.IsInRole("CEO") || User.IsInRole("FOUNDER"),
                JustCreatedCode = TempData["JustCreatedCode"] as string
            };

            foreach (var u in _users.Users.OrderBy(u => u.UserName).ToList())
            {
                var claims = await _users.GetClaimsAsync(u);
                var roles = (await _users.GetRolesAsync(u)).ToList();
                var row = new AdminUserRow
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    Nombres = claims.FirstOrDefault(c => c.Type == "Nombres")?.Value ?? "—",
                    Roles = string.Join(", ", roles),
                    IsBanned = u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow,
                    IsSelf = u.Id == _users.GetUserId(User)
                };

                if (roles.Contains("PUBLISHER"))
                    vm.Publishers.Add(row);
                else if (roles.Contains("CEO") || roles.Contains("FOUNDER") || roles.Contains("INTERNO"))
                    vm.Internos.Add(row);
                else
                    vm.Usuarios.Add(row);
            }

            if (vm.CanManageKeys)
            {
                var usedNames = _users.Users.ToDictionary(u => u.Id, u => u.UserName ?? u.Email ?? "?");
                foreach (var k in _db.InviteKeys.OrderByDescending(k => k.Id).Take(50).ToList())
                {
                    vm.Keys.Add(new AdminKeyRow
                    {
                        Id = k.Id,
                        Code = k.Code,
                        IsActive = k.IsActive,
                        CreatedAtUtc = k.CreatedAtUtc,
                        UsedBy = k.UsedByUserId != null && usedNames.TryGetValue(k.UsedByUserId, out var n) ? n : string.Empty
                    });
                }
            }

            return View(vm);
        }

        // Solo CEO y FOUNDER generan keys de invitación.
        [HttpPost]
        [Authorize(Roles = "CEO,FOUNDER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateKey()
        {
            string code;
            do
            {
                code = "INN-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            } while (_db.InviteKeys.Any(k => k.Code == code));

            var userId = _users.GetUserId(User);
            _db.InviteKeys.Add(new InviteKey
            {
                Code = code,
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                IsActive = true
            });
            await _db.SaveChangesAsync();

            TempData["JustCreatedCode"] = code;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "CEO,FOUNDER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateKey(int id)
        {
            var key = _db.InviteKeys.FirstOrDefault(k => k.Id == id);
            if (key != null)
            {
                key.IsActive = false;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // Borrado permanente de la key.
        [HttpPost]
        [Authorize(Roles = "CEO,FOUNDER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteKey(int id)
        {
            var key = _db.InviteKeys.FirstOrDefault(k => k.Id == id);
            if (key != null)
            {
                _db.InviteKeys.Remove(key);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // Banear: bloqueo permanente + expulsa sesiones activas.
        // Solo CEO/FOUNDER. Nadie puede banearse a sí mismo.
        [HttpPost]
        [Authorize(Roles = "CEO,FOUNDER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanUser(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user != null && user.Id != _users.GetUserId(User))
            {
                await _users.SetLockoutEnabledAsync(user, true);
                await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                await _users.UpdateSecurityStampAsync(user);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "CEO,FOUNDER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnbanUser(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user != null)
            {
                await _users.SetLockoutEndDateAsync(user, null);
                await _users.UpdateSecurityStampAsync(user);
            }
            return RedirectToAction(nameof(Index));
        }

        // Eliminar cuenta: borra el usuario y revoca sus tokens del launcher.
        // Solo CEO/FOUNDER. Nadie puede eliminarse a sí mismo.
        // Su historial (pedidos, keys, sesiones) se conserva con autor "—".
        [HttpPost]
        [Authorize(Roles = "CEO,FOUNDER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var me = _users.GetUserId(User);
            if (!string.IsNullOrEmpty(id) && id != me)
            {
                var user = await _users.FindByIdAsync(id);
                if (user != null)
                {
                    var tokens = _db.LauncherTokens.Where(t => t.UserId == id).ToList();
                    if (tokens.Count > 0)
                    {
                        _db.LauncherTokens.RemoveRange(tokens);
                        await _db.SaveChangesAsync();
                    }
                    await _users.DeleteAsync(user);
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // ---- Horas de juego (reportadas por el launcher) ----

        public IActionResult Horas()
        {
            var nombres = _users.Users.ToDictionary(u => u.Id, u => u.UserName ?? u.Email ?? "?");
            var vm = new HorasViewModel();
            foreach (var g in _db.PlaySessions.AsEnumerable()
                .GroupBy(s => new { s.UserId, s.GameSlug })
                .OrderByDescending(g => g.Sum(s => s.Seconds)))
            {
                vm.Filas.Add(new HorasRow
                {
                    UserName = nombres.TryGetValue(g.Key.UserId, out var n) ? n : "?",
                    GameTitle = g.Select(s => s.GameTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? g.Key.GameSlug,
                    GameSlug = g.Key.GameSlug,
                    TotalSeconds = g.Sum(s => s.Seconds),
                    Sessions = g.Count(),
                    LastPlayedUtc = g.Max(s => s.EndedAtUtc ?? s.StartedAtUtc)
                });
            }
            return View(vm);
        }

        // ---- Noticias ----

        public IActionResult Noticias()
        {
            var items = _db.NewsItems.OrderByDescending(n => n.CreatedAtUtc).Take(50).ToList();
            return View(items);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            return View(new CrearNoticiaViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(CrearNoticiaViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            string? imagePath = null;
            if (m.Image != null && m.Image.Length > 0)
            {
                var ext = Path.GetExtension(m.Image.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!allowed.Contains(ext) || !m.Image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("Image", "Solo imágenes JPG, PNG, GIF o WEBP.");
                    return View(m);
                }
                if (m.Image.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("Image", "Máximo 5 MB.");
                    return View(m);
                }

                var dir = Path.Combine(_env.WebRootPath, "uploads", "news");
                Directory.CreateDirectory(dir);
                var fileName = Guid.NewGuid().ToString("N") + ext;
                await using var stream = System.IO.File.Create(Path.Combine(dir, fileName));
                await m.Image.CopyToAsync(stream);
                imagePath = "/uploads/news/" + fileName;
            }

            var link = (m.LinkUrl ?? string.Empty).Trim();
            if (link.Length > 0 && !link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                link = "https://" + link;
            if (!string.IsNullOrEmpty(link) && !Uri.TryCreate(link, UriKind.Absolute, out _))
            {
                ModelState.AddModelError("LinkUrl", "Enlace no válido.");
                return View(m);
            }

            _db.NewsItems.Add(new NewsItem
            {
                Title = m.Title.Trim(),
                Body = m.Body.Trim(),
                ImagePath = imagePath,
                LinkUrl = string.IsNullOrEmpty(link) ? null : link,
                LinkText = string.IsNullOrWhiteSpace(m.LinkText) ? null : m.LinkText.Trim(),
                IsPublished = m.IsPublished,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = _users.GetUserId(User)
            });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Noticias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleNoticia(int id)
        {
            var item = _db.NewsItems.FirstOrDefault(n => n.Id == id);
            if (item != null)
            {
                item.IsPublished = !item.IsPublished;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Noticias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNoticia(int id)
        {
            var item = _db.NewsItems.FirstOrDefault(n => n.Id == id);
            if (item != null)
            {
                if (!string.IsNullOrEmpty(item.ImagePath))
                {
                    try
                    {
                        var full = Path.Combine(_env.WebRootPath, item.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(full))
                            System.IO.File.Delete(full);
                    }
                    catch { /* mejor esfuerzo */ }
                }
                _db.NewsItems.Remove(item);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Noticias));
        }

        // ---- Launcher (builds descargables desde Plataforma) ----

        private static readonly string[] LauncherExtensions = { ".zip", ".exe", ".msi" };

        private const long MaxLauncherBytes = 512L * 1024 * 1024;

        public IActionResult Launcher()
        {
            return VistaLauncher(new SubirLauncherViewModel());
        }

        private IActionResult VistaLauncher(SubirLauncherViewModel m)
        {
            ViewData["Builds"] = _db.LauncherBuilds.OrderByDescending(b => b.Id).Take(20).ToList();
            return View("Launcher", m);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxLauncherBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxLauncherBytes)]
        public async Task<IActionResult> SubirLauncher(SubirLauncherViewModel m)
        {
            if (m.Archivo == null || m.Archivo.Length == 0)
                ModelState.AddModelError("Archivo", "Selecciona el archivo del launcher (.zip, .exe o .msi).");
            else
            {
                var ext = Path.GetExtension(m.Archivo.FileName).ToLowerInvariant();
                if (!LauncherExtensions.Contains(ext))
                    ModelState.AddModelError("Archivo", "Solo archivos .zip, .exe o .msi.");
                else if (m.Archivo.Length > MaxLauncherBytes)
                    ModelState.AddModelError("Archivo", "Máximo 512 MB.");
            }
            if (!ModelState.IsValid)
                return VistaLauncher(m);

            var dir = Path.Combine(_env.WebRootPath, "uploads", "launcher");
            Directory.CreateDirectory(dir);

            string? shotPath = null;
            if (m.Screenshot != null && m.Screenshot.Length > 0)
            {
                var shotExt = Path.GetExtension(m.Screenshot.FileName).ToLowerInvariant();
                var shotAllowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!shotAllowed.Contains(shotExt) || !m.Screenshot.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("Screenshot", "Solo imágenes JPG, PNG, GIF o WEBP.");
                    return VistaLauncher(m);
                }
                if (m.Screenshot.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("Screenshot", "Máximo 5 MB.");
                    return VistaLauncher(m);
                }
                var shotName = Guid.NewGuid().ToString("N") + shotExt;
                await using (var stream = System.IO.File.Create(Path.Combine(dir, shotName)))
                    await m.Screenshot.CopyToAsync(stream);
                shotPath = "/uploads/launcher/" + shotName;
            }
            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(m.Archivo!.FileName).ToLowerInvariant();
            await using (var stream = System.IO.File.Create(Path.Combine(dir, fileName)))
                await m.Archivo.CopyToAsync(stream);

            _db.LauncherBuilds.Add(new LauncherBuild
            {
                Version = m.Version.Trim(),
                Description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim(),
                Specs = string.IsNullOrWhiteSpace(m.Specs) ? null : m.Specs.Trim(),
                ScreenshotPath = shotPath,
                Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes.Trim(),
                FilePath = "/uploads/launcher/" + fileName,
                FileName = Path.GetFileName(m.Archivo.FileName),
                SizeBytes = m.Archivo.Length,
                IsPublished = m.IsPublished,
                Downloads = 0,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = _users.GetUserId(User)
            });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Launcher));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLauncher(int id)
        {
            var build = _db.LauncherBuilds.FirstOrDefault(b => b.Id == id);
            if (build != null)
            {
                build.IsPublished = !build.IsPublished;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Launcher));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLauncher(int id)
        {
            var build = _db.LauncherBuilds.FirstOrDefault(b => b.Id == id);
            if (build != null)
            {
                foreach (var rel in new[] { build.FilePath, build.ScreenshotPath })
                {
                    if (string.IsNullOrEmpty(rel))
                        continue;
                    try
                    {
                        var full = Path.Combine(_env.WebRootPath, rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(full))
                            System.IO.File.Delete(full);
                    }
                    catch { /* mejor esfuerzo */ }
                }
                _db.LauncherBuilds.Remove(build);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Launcher));
        }

        // ---- STORE (kit de publicación de juegos) ----
        // Todos los campos del kit son obligatorios: portada, título,
        // descripción, requisitos mínimos y recomendados, precio,
        // screenshots (1-6) y tráiler. Preventa y Acceso anticipado
        // se responden con check (No/No = lanzamiento normal) y son
        // excluyentes entre sí.

        private static readonly string[] StoreImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        private const long MaxStoreImageBytes = 5L * 1024 * 1024;

        private const int MaxStoreShots = 6;

        public IActionResult Store()
        {
            return VistaStore(new PublicarStoreViewModel());
        }

        private IActionResult VistaStore(PublicarStoreViewModel m)
        {
            ViewData["Games"] = _db.StoreGames.OrderByDescending(g => g.Id).Take(50).ToList();
            ViewData["Orders"] = _db.StoreOrders.OrderByDescending(o => o.Id).Take(50).ToList();
            ViewData["BuyerNames"] = _users.Users.ToDictionary(u => u.Id, u => u.UserName ?? u.Email ?? "?");
            return View("Store", m);
        }

        private static bool EsImagenTienda(string fileName, string contentType, out string ext)
        {
            ext = Path.GetExtension(fileName).ToLowerInvariant();
            return StoreImageExtensions.Contains(ext)
                && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        private void BorrarArchivoTienda(string? rel)
        {
            if (string.IsNullOrEmpty(rel))
                return;
            try
            {
                var full = Path.Combine(_env.WebRootPath, rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* mejor esfuerzo */ }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublicarStore(PublicarStoreViewModel m)
        {
            if (m.IsPreorder && m.IsEarlyAccess)
                ModelState.AddModelError(string.Empty, "No puede ser preventa y acceso anticipado a la vez.");

            if (m.Cover == null || m.Cover.Length == 0)
                ModelState.AddModelError("Cover", "La portada es obligatoria.");
            else if (!EsImagenTienda(m.Cover.FileName, m.Cover.ContentType, out _))
                ModelState.AddModelError("Cover", "Solo imágenes JPG, PNG, GIF o WEBP.");
            else if (m.Cover.Length > MaxStoreImageBytes)
                ModelState.AddModelError("Cover", "Máximo 5 MB.");

            var shots = (m.Screenshots ?? new List<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
            if (shots.Count == 0)
                ModelState.AddModelError("Screenshots", "Sube al menos 1 screenshot (máximo 6).");
            else if (shots.Count > MaxStoreShots)
                ModelState.AddModelError("Screenshots", "Máximo 6 screenshots.");
            else
            {
                foreach (var s in shots)
                {
                    if (!EsImagenTienda(s.FileName, s.ContentType, out _))
                    {
                        ModelState.AddModelError("Screenshots", "Solo imágenes JPG, PNG, GIF o WEBP.");
                        break;
                    }
                    if (s.Length > MaxStoreImageBytes)
                    {
                        ModelState.AddModelError("Screenshots", "Cada screenshot: máximo 5 MB.");
                        break;
                    }
                }
            }

            var trailer = (m.TrailerUrl ?? string.Empty).Trim();
            if (trailer.Length > 0 && !trailer.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                trailer = "https://" + trailer;
            if (!string.IsNullOrEmpty(trailer) && !Uri.TryCreate(trailer, UriKind.Absolute, out _))
                ModelState.AddModelError("TrailerUrl", "Tráiler no válido (URL de YouTube o MP4).");
            else if (!string.IsNullOrEmpty(trailer))
                m.TrailerUrl = trailer;

            if (!ModelState.IsValid)
                return VistaStore(m);

            var dir = Path.Combine(_env.WebRootPath, "uploads", "store");
            Directory.CreateDirectory(dir);

            var coverExt = Path.GetExtension(m.Cover!.FileName).ToLowerInvariant();
            var coverName = "cover-" + Guid.NewGuid().ToString("N") + coverExt;
            await using (var stream = System.IO.File.Create(Path.Combine(dir, coverName)))
                await m.Cover.CopyToAsync(stream);
            var coverPath = "/uploads/store/" + coverName;

            var shotPaths = new List<string>();
            foreach (var s in shots)
            {
                var shotName = "shot-" + Guid.NewGuid().ToString("N") + Path.GetExtension(s.FileName).ToLowerInvariant();
                await using (var stream = System.IO.File.Create(Path.Combine(dir, shotName)))
                    await s.CopyToAsync(stream);
                shotPaths.Add("/uploads/store/" + shotName);
            }

            _db.StoreGames.Add(new StoreGame
            {
                Title = m.Title.Trim(),
                Description = m.Description.Trim(),
                MinRequirements = m.MinRequirements.Trim(),
                RecRequirements = m.RecRequirements.Trim(),
                IsPreorder = m.IsPreorder,
                IsEarlyAccess = m.IsEarlyAccess,
                Price = m.Price!.Value,
                CoverPath = coverPath,
                ScreenshotsJson = JsonSerializer.Serialize(shotPaths),
                TrailerUrl = trailer,
                IsPublished = m.IsPublished,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = _users.GetUserId(User)
            });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Store));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStore(int id)
        {
            var game = _db.StoreGames.FirstOrDefault(g => g.Id == id);
            if (game != null)
            {
                game.IsPublished = !game.IsPublished;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Store));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStore(int id)
        {
            var game = _db.StoreGames.FirstOrDefault(g => g.Id == id);
            if (game != null)
            {
                BorrarArchivoTienda(game.CoverPath);
                try
                {
                    var shots = JsonSerializer.Deserialize<List<string>>(game.ScreenshotsJson ?? "[]") ?? new();
                    foreach (var s in shots)
                        BorrarArchivoTienda(s);
                }
                catch { /* mejor esfuerzo */ }
                _db.StoreGames.Remove(game);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Store));
        }

        // Pedidos de pago manual: el equipo marca Pagado al validar el pago.
        // Al marcar Pagado se genera la key única del juego (XXXXX-XXXXX-XXXXX,
        // 17 caracteres con guiones) si el pedido aún no tiene una.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleOrder(int id)
        {
            var order = _db.StoreOrders.FirstOrDefault(o => o.Id == id);
            if (order != null)
            {
                if (order.Status == "Pagado")
                {
                    order.Status = "Pendiente";
                }
                else
                {
                    order.Status = "Pagado";
                    if (string.IsNullOrEmpty(order.KeyCode))
                        order.KeyCode = GenerarKeyJuegoUnica();
                }
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Store));
        }

        // Borrar pedido permanentemente (incluye su key si tenía).
        // Solo CEO/FOUNDER.
        [HttpPost]
        [Authorize(Roles = "CEO,FOUNDER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = _db.StoreOrders.FirstOrDefault(o => o.Id == id);
            if (order != null)
            {
                _db.StoreOrders.Remove(order);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Store));
        }

        // Alfabeto sin caracteres ambiguos (sin 0/O, 1/I/L).
        private static readonly char[] KeyAlphabet =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        // Key estilo XXXXX-XXXXX-XXXXX (15 caracteres + 2 guiones = 17).
        private static string GenerarKeyJuego()
        {
            var bytes = RandomNumberGenerator.GetBytes(15);
            var chars = new char[17];
            for (int i = 0; i < 15; i++)
                chars[i + (i / 5)] = KeyAlphabet[bytes[i] % KeyAlphabet.Length];
            chars[5] = '-';
            chars[11] = '-';
            return new string(chars);
        }

        private string GenerarKeyJuegoUnica()
        {
            string key;
            do
            {
                key = GenerarKeyJuego();
            } while (_db.StoreOrders.Any(o => o.KeyCode == key));
            return key;
        }

        // ---- USUARIOS: biblioteca de juegos canjeados por usuario ----
        // Lista todos los usuarios del launcher (cuentas Identity, creadas
        // desde la web o desde api/launcher). Clic → detalle con sus juegos
        // canjeados y la key usada en cada uno.

        public async Task<IActionResult> Usuarios()
        {
            var ordersByUser = _db.StoreOrders.AsEnumerable()
                .Where(o => o.BuyerUserId != null)
                .GroupBy(o => o.BuyerUserId!)
                .ToDictionary(
                    g => g.Key,
                    g => new { Juegos = g.Select(o => o.GameId).Distinct().Count(), Keys = g.Count(o => o.KeyCode != null) });

            var vm = new AdminUsuariosViewModel();
            foreach (var u in _users.Users.OrderBy(u => u.UserName).ToList())
            {
                ordersByUser.TryGetValue(u.Id, out var stats);
                vm.Users.Add(new AdminLibraryUserRow
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    Roles = string.Join(", ", await _users.GetRolesAsync(u)),
                    GamesCount = stats?.Juegos ?? 0,
                    KeysCount = stats?.Keys ?? 0
                });
            }
            return View(vm);
        }

        public async Task<IActionResult> UsuarioDetalle(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null)
                return RedirectToAction(nameof(Usuarios));
            var vm = new AdminUsuarioDetalleViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Roles = string.Join(", ", await _users.GetRolesAsync(user)),
                Orders = _db.StoreOrders
                    .Where(o => o.BuyerUserId == id)
                    .OrderByDescending(o => o.Id)
                    .ToList()
            };
            return View(vm);
        }
    }
}
