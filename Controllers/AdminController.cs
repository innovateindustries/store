using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

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
    }
}
