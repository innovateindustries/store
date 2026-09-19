using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Controllers.Api
{
    // API JSON de staff para el panel ADMIN del launcher.
    // Todo exige rol CEO/FOUNDER/INTERNO (el token opaco ya trae la identidad).
    [ApiController]
    [Route("api/admin")]
    public class LauncherAdminApiController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public LauncherAdminApiController(UserManager<IdentityUser> users, AppDbContext db, IWebHostEnvironment env)
        {
            _users = users;
            _db = db;
            _env = env;
        }

        private static string Hash(string token)
        {
            var b = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("inn-launcher:" + token));
            return Convert.ToHexString(b);
        }

        private async Task<IdentityUser?> UsuarioPorTokenAsync()
        {
            var h = Request.Headers.Authorization.ToString();
            if (!h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;
            var hash = Hash(h["Bearer ".Length..].Trim());
            var tok = await _db.LauncherTokens.FirstOrDefaultAsync(t =>
                t.TokenHash == hash && !t.Revoked && t.ExpiresAtUtc > DateTime.UtcNow);
            if (tok == null)
                return null;
            return await _users.FindByIdAsync(tok.UserId);
        }

        private async Task<bool> EsStaffAsync(IdentityUser u)
        {
            var roles = await _users.GetRolesAsync(u);
            return roles.Contains("CEO") || roles.Contains("FOUNDER") || roles.Contains("INTERNO");
        }

        private async Task<IActionResult?> SoloStaffAsync()
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)
                return StatusCode(403, new { banned = true });
            if (!await EsStaffAsync(user))
                return StatusCode(403);
            return null;
        }

        // ---------- Usuarios ----------

        [HttpGet("users")]
        public async Task<IActionResult> Usuarios()
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            var puntos = _db.LauncherPoints.ToDictionary(p => p.UserId, p => p.Points);
            var perfiles = _db.LauncherProfiles.ToDictionary(p => p.UserId, p => p.ImagePath);
            var libs = _db.StoreOrders
                .Where(o => o.Status == "Pagado" && (o.RedeemedByUserId != null || o.BuyerUserId != null))
                .AsEnumerable()
                .GroupBy(o => o.RedeemedByUserId ?? o.BuyerUserId!)
                .ToDictionary(g => g.Key, g => g.Select(o => o.GameId).Distinct().Count());
            var lista = new List<object>();
            foreach (var u in _users.Users.OrderBy(u => u.UserName).Take(500).ToList())
            {
                var roles = string.Join(",", await _users.GetRolesAsync(u));
                lista.Add(new
                {
                    id = u.Id,
                    userName = u.UserName ?? string.Empty,
                    email = u.Email ?? string.Empty,
                    roles,
                    banned = u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow,
                    createdAt = (DateTime?)null,
                    library = libs.TryGetValue(u.Id, out var n) ? n : 0,
                    points = puntos.TryGetValue(u.Id, out var p) ? p : 0,
                    profileImagePath = perfiles.TryGetValue(u.Id, out var img) ? img : string.Empty
                });
            }
            return Ok(new { users = lista });
        }

        // ---------- Órdenes ----------

        [HttpGet("orders")]
        public async Task<IActionResult> Ordenes()
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            var ordenes = _db.StoreOrders.OrderByDescending(o => o.CreatedAtUtc).Take(200).ToList();
            var nombres = _users.Users.ToDictionary(u => u.Id, u => u.UserName ?? u.Email ?? "?");
            return Ok(new
            {
                orders = ordenes.ConvertAll(o => new
                {
                    id = o.Id,
                    reference = o.Reference,
                    status = o.Status,
                    amount = o.Price,
                    amountText = o.Price <= 0 ? "FREE" : "$" + o.Price.ToString("0.##"),
                    gameTitle = o.GameTitle,
                    userName = o.BuyerUserId != null && nombres.TryGetValue(o.BuyerUserId, out var n) ? n : string.Empty,
                    discordUser = (string?)null,
                    nota = (string?)null,
                    createdAt = o.CreatedAtUtc,
                    updatedAt = o.CreatedAtUtc
                })
            });
        }

        // Confirma el pago: Pendiente → Pagado + key + puntos (1 USD = 100 pts).
        [HttpPost("orders/{id:int}/confirm")]
        public async Task<IActionResult> ConfirmarOrden(int id, [FromBody] LauncherConfirmOrderRequest m)
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            var order = _db.StoreOrders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound(new { error = "ORDER_NOT_FOUND" });
            if (string.Equals(order.Status, "Cancelado", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "ORDER_CANCELLED" });
            if (!string.IsNullOrEmpty(order.RedeemedByUserId))
                return Conflict(new { error = "ORDER_REDEEMED" });
            if (string.IsNullOrEmpty(order.KeyCode))
            {
                order.KeyCode = GenerarKeyJuegoUnica();
                order.Status = "Pagado";
                // 1 USD = 100 pts al confirmar pago.
                if (!string.IsNullOrEmpty(order.BuyerUserId) && order.Price > 0)
                    await SumarPuntosAsync(order.BuyerUserId, (int)Math.Round(order.Price * 100m));
            }
            else if (!string.Equals(order.Status, "Pagado", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "Pagado";
            }
            await _db.SaveChangesAsync();
            return Ok(new { ok = true, status = order.Status, key = order.KeyCode ?? string.Empty, reference = order.Reference });
        }

        [HttpPost("orders/{id:int}/cancel")]
        public async Task<IActionResult> CancelarOrden(int id)
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            var order = _db.StoreOrders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound(new { error = "ORDER_NOT_FOUND" });
            if (!string.IsNullOrEmpty(order.RedeemedByUserId))
                return Conflict(new { error = "ORDER_REDEEMED" });
            order.Status = "Cancelado";
            order.KeyCode = null;
            await _db.SaveChangesAsync();
            return Ok(new { ok = true, status = order.Status });
        }

        [HttpDelete("orders/{id:int}")]
        public async Task<IActionResult> BorrarOrden(int id)
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            var order = _db.StoreOrders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound(new { error = "ORDER_NOT_FOUND" });
            _db.StoreOrders.Remove(order);
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        private async Task SumarPuntosAsync(string userId, int amount)
        {
            if (amount == 0) return;
            var row = await _db.LauncherPoints.FirstOrDefaultAsync(p => p.UserId == userId);
            if (row == null)
            {
                row = new LauncherPoints { UserId = userId, Points = 0, UpdatedAtUtc = DateTime.UtcNow };
                _db.LauncherPoints.Add(row);
            }
            row.Points = Math.Max(0, row.Points + amount);
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        private static readonly char[] KeyAlphabet =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

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

        // ---------- Publicación ADMIN → STORE (todo obligatorio) ----------

        [HttpPost("games/publish")]
        public async Task<IActionResult> PublicarJuego([FromBody] LauncherPublishGameRequest m)
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            var shots = m.ScreenshotsB64?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new();
            var title = (m.Title ?? string.Empty).Trim();
            var genre = (m.Genre ?? string.Empty).Trim();
            var description = (m.Description ?? string.Empty).Trim();
            var trailer = (m.TrailerUrl ?? string.Empty).Trim();
            var regions = (m.Regions ?? new()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
            var valido = title.Length >= 3
                && genre.Length > 0
                && description.Length > 0
                && trailer.Length > 0
                && !string.IsNullOrWhiteSpace(m.CoverB64) && m.CoverB64.Length >= 60
                && shots.Count >= 6
                && regions.Count > 0;
            if (!valido)
                return BadRequest(new { error = "MISSING_FIELDS" });

            StoreGame? g = m.Id != null ? _db.StoreGames.FirstOrDefault(x => x.Id == m.Id) : null;
            if (g == null)
            {
                g = new StoreGame { Title = title, CreatedAtUtc = DateTime.UtcNow };
                _db.StoreGames.Add(g);
            }
            g.Title = title;
            g.Genre = genre;
            g.Price = m.Price;
            g.Description = description;
            g.IsPreorder = m.IsPreorder;
            g.InPass = m.InPass;
            g.Regions = string.Join(",", regions.Distinct());
            g.TrailerUrl = trailer;
            g.IsPublished = true;

            var slug = SlugDe(g.Title);
            var dir = Path.Combine(_env.WebRootPath, "uploads", "store");
            Directory.CreateDirectory(dir);
            async Task<string> Guardar(string b64, string nombre)
            {
                var clean = b64.Contains(',') ? b64[(b64.IndexOf(',') + 1)..] : b64;
                var bytes = Convert.FromBase64String(clean);
                var archivo = slug + "-" + nombre + ".png";
                await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, archivo), bytes);
                return "/uploads/store/" + archivo;
            }
            // Borra imágenes anteriores al actualizar (mejor esfuerzo).
            foreach (var vieja in ScreenshotsViejos(g).Prepend(g.CoverPath))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(vieja)) continue;
                    var full = Path.Combine(_env.WebRootPath,
                        vieja.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                }
                catch { /* mejor esfuerzo */ }
            }
            g.CoverPath = await Guardar(m.CoverB64!, "cover");
            var nuevas = new List<string>();
            for (int i = 0; i < Math.Min(shots.Count, 6); i++)
                nuevas.Add(await Guardar(shots[i], "shot" + (i + 1)));
            g.ScreenshotsJson = JsonSerializer.Serialize(nuevas);

            await _db.SaveChangesAsync();
            return Ok(new { ok = true, gameId = g.Id, title = g.Title });
        }

        private static List<string> ScreenshotsViejos(StoreGame g)
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(g.ScreenshotsJson ?? "[]") ?? new();
            }
            catch { return new(); }
        }

        private static string SlugDe(string titulo)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in titulo.ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            var s = sb.ToString().Trim('-');
            while (s.Contains("--")) s = s.Replace("--", "-");
            return string.IsNullOrWhiteSpace(s) ? "juego" : s;
        }

        // ---------- Puntos ----------

        [HttpPost("points/grant/{username}")]
        public async Task<IActionResult> OtorgarPuntos(string username, [FromBody] LauncherGrantPointsRequest m)
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            if (m.Amount == 0)
                return BadRequest(new { error = "AMOUNT_REQUIRED" });
            var target = await _users.FindByNameAsync(username);
            if (target == null)
                return NotFound(new { error = "USER_NOT_FOUND" });
            await SumarPuntosAsync(target.Id, m.Amount);
            await _db.SaveChangesAsync();
            var pts = (await _db.LauncherPoints.FirstOrDefaultAsync(p => p.UserId == target.Id))?.Points ?? 0;
            return Ok(new { ok = true, userName = target.UserName, points = pts });
        }

        // ---------- Distribución: subida de archivos + publicación de build ----------

        [HttpPost("games/{id:int}/files")]
        [RequestSizeLimit(512L * 1024 * 1024)]
        public async Task<IActionResult> SubirArchivoBuild(int id)
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            var game = _db.StoreGames.FirstOrDefault(g => g.Id == id);
            if (game == null)
                return NotFound(new { error = "GAME_NOT_FOUND" });
            var v = (Request.Query["v"].ToString() ?? string.Empty).Trim();
            var rel = (Request.Query["path"].ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(v) || string.IsNullOrWhiteSpace(rel))
                return BadRequest();
            var raiz = Path.Combine(_env.WebRootPath, "uploads", "builds", id.ToString(), v);
            var dest = RutaSegura(raiz, rel);
            if (dest == null)
                return BadRequest(new { error = "BAD_PATH" });
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await using (var fs = System.IO.File.Create(dest))
                await Request.Body.CopyToAsync(fs, HttpContext.RequestAborted);
            return Ok(new { ok = true, path = rel, bytes = new FileInfo(dest).Length });
        }

        [HttpPost("games/{id:int}/publish")]
        public async Task<IActionResult> PublicarBuild(int id, [FromBody] LauncherPublishBuildRequest m)
        {
            var denegado = await SoloStaffAsync();
            if (denegado != null) return denegado;
            var game = _db.StoreGames.FirstOrDefault(g => g.Id == id);
            if (game == null)
                return NotFound(new { error = "GAME_NOT_FOUND" });
            var version = (m.Version ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(version) || m.Files == null || m.Files.Count == 0)
                return BadRequest(new { error = "BAD_MANIFEST" });
            var raiz = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "builds", id.ToString(), version));
            long total = 0;
            foreach (var f in m.Files)
            {
                if (string.IsNullOrWhiteSpace(f.Path))
                    return BadRequest(new { error = "BAD_MANIFEST" });
                var dest = RutaSegura(raiz, f.Path);
                if (dest == null || !System.IO.File.Exists(dest))
                    return BadRequest(new { error = "MISSING_FILE: " + f.Path });
                if (f.Size > 0 && new FileInfo(dest).Length != f.Size)
                    return BadRequest(new { error = "SIZE_MISMATCH: " + f.Path });
                total += f.Size;
            }
            var manifest = new
            {
                slug = SlugDe(game.Title),
                version,
                builtAt = DateTime.UtcNow,
                totalSize = total,
                exe = m.Exe ?? string.Empty,
                files = m.Files.ConvertAll(f => new { path = f.Path, size = f.Size, sha256 = f.Sha256 ?? string.Empty, url = string.Empty })
            };
            var existente = _db.GameBuilds.FirstOrDefault(b => b.GameId == id);
            var staffId = (await UsuarioPorTokenAsync())?.Id;
            if (existente == null)
            {
                existente = new GameBuild { GameId = id, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = staffId };
                _db.GameBuilds.Add(existente);
            }
            existente.Version = version;
            existente.Exe = m.Exe ?? string.Empty;
            existente.ManifestJson = JsonSerializer.Serialize(manifest);
            existente.TotalSize = total;
            await _db.SaveChangesAsync();
            return Ok(new { ok = true, gameId = id, version, totalSize = total });
        }

        private static string? RutaSegura(string raiz, string rel)
        {
            try
            {
                var full = Path.GetFullPath(Path.Combine(raiz, rel.Replace('/', Path.DirectorySeparatorChar)));
                var raizFull = Path.GetFullPath(raiz);
                return full.StartsWith(raizFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    ? full : null;
            }
            catch { return null; }
        }
    }
}
