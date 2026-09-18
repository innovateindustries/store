using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Controllers.Api
{
    // API JSON para el launcher de escritorio (tokens, no cookies).
    [ApiController]
    [Route("api/launcher")]
    public class LauncherApiController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly AppDbContext _db;

        public LauncherApiController(UserManager<IdentityUser> users, AppDbContext db)
        {
            _users = users;
            _db = db;
        }

        private static string Hash(string token)
        {
            var b = SHA256.HashData(Encoding.UTF8.GetBytes("inn-launcher:" + token));
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

        private static bool Baneado(IdentityUser u) =>
            u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow;

        // Registro desde el launcher: crea el usuario web (username + email).
        // Plan PUBLISHER otorga ese rol (sin acceso admin). TRABAJADOR lo da un admin.
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] LauncherRegisterRequest m)
        {
            var username = (m.Username ?? string.Empty).Trim();
            var email = (m.Email ?? string.Empty).Trim();
            var pass = m.Password ?? string.Empty;
            var plan = (m.Plan ?? "USUARIO").Trim().ToUpperInvariant();

            if (username.Length < 3 || username.Any(char.IsWhiteSpace))
                return BadRequest(new { errors = new[] { "Usuario inválido (mínimo 3 caracteres, sin espacios)." } });
            if (!new EmailAddressAttribute().IsValid(email))
                return BadRequest(new { errors = new[] { "Email no válido." } });
            if (pass.Length < 6)
                return BadRequest(new { errors = new[] { "La clave debe tener mínimo 6 caracteres." } });
            if (await _users.FindByNameAsync(username) != null)
                return BadRequest(new { errors = new[] { "Ese usuario ya existe en la web." } });
            if (await _users.FindByEmailAsync(email) != null)
                return BadRequest(new { errors = new[] { "Ese email ya está registrado en la web." } });

            var user = new IdentityUser { UserName = username, Email = email };
            var r = await _users.CreateAsync(user, pass);
            if (!r.Succeeded)
                return BadRequest(new { errors = r.Errors.Select(e => e.Description).ToArray() });
            if (plan == "PUBLISHER")
                await _users.AddToRoleAsync(user, "PUBLISHER");
            return StatusCode(201, new { userName = user.UserName });
        }

        // Registro interno desde el launcher: exige key de invitacion activa (igual que la web).
        // La contrasena ES la key (mayusculas), rol INTERNO, key consumida (un solo uso).
        [HttpPost("register-internal")]
        public async Task<IActionResult> RegisterInternal([FromBody] LauncherInternalRegisterRequest m)
        {
            var username = (m.Username ?? string.Empty).Trim();
            var email = (m.WorkEmail ?? string.Empty).Trim();
            var nombres = (m.Nombres ?? string.Empty).Trim();
            var code = (m.Key ?? string.Empty).Trim().ToUpperInvariant();

            if (username.Length < 3 || username.Any(char.IsWhiteSpace))
                return BadRequest(new { errors = new[] { "Usuario invalido (minimo 3 caracteres, sin espacios)." } });
            if (!new EmailAddressAttribute().IsValid(email))
                return BadRequest(new { errors = new[] { "Email no valido." } });
            if (nombres.Length < 2)
                return BadRequest(new { errors = new[] { "Indica tus nombres." } });
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { errors = new[] { "Key de interno requerida." } });

            var key = _db.InviteKeys.FirstOrDefault(k => k.Code == code && k.IsActive && k.UsedByUserId == null);
            if (key is null)
                return BadRequest(new { errors = new[] { "Key no valida, ya usada o desactivada." } });

            if (await _users.FindByNameAsync(username) != null)
                return BadRequest(new { errors = new[] { "Ese usuario ya existe en la web." } });
            if (await _users.FindByEmailAsync(email) != null)
                return BadRequest(new { errors = new[] { "Ese email ya esta registrado en la web." } });

            var user = new IdentityUser { UserName = username, Email = email };
            var r = await _users.CreateAsync(user, code);
            if (!r.Succeeded)
                return BadRequest(new { errors = r.Errors.Select(e => e.Description).ToArray() });

            await _users.AddClaimAsync(user, new Claim("Nombres", nombres));
            await _users.AddToRoleAsync(user, "INTERNO");

            key.UsedByUserId = user.Id;
            key.UsedAtUtc = DateTime.UtcNow;
            key.IsActive = false;
            await _db.SaveChangesAsync();

            return StatusCode(201, new { userName = user.UserName });
        }

        // Diagnóstico sin auth (el launcher lo usa en AJUSTES → probar conexión).
        [HttpGet("ping")]
        public IActionResult Ping() => Ok(new { ok = true, utc = DateTime.UtcNow });

        // Acepta email o nombre de usuario (igual que el login web).
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LauncherLoginRequest m)
        {
            var id = (m.Identifier ?? string.Empty).Trim();
            if (id.Length == 0 || string.IsNullOrEmpty(m.Password))
                return Unauthorized(new { error = "Credenciales incompletas." });
            var user = await _users.FindByEmailAsync(id) ?? await _users.FindByNameAsync(id);
            if (user == null)
                return Unauthorized(new { error = "Credenciales incorrectas." });
            if (!await _users.CheckPasswordAsync(user, m.Password))
                return Unauthorized(new { error = "Credenciales incorrectas." });
            if (Baneado(user))
                return StatusCode(403, new { banned = true });

            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _db.LauncherTokens.Add(new LauncherToken
            {
                UserId = user.Id,
                TokenHash = Hash(raw),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                Revoked = false
            });
            await _db.SaveChangesAsync();

            var roles = (await _users.GetRolesAsync(user)).ToList();
            return Ok(new LauncherLoginResponse
            {
                Token = raw,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Roles = roles
            });
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var roles = (await _users.GetRolesAsync(user)).ToList();
            return Ok(new { userName = user.UserName, email = user.Email, roles });
        }

        // Latido cada ~60s mientras se juega. Crea o extiende la sesión abierta.
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat([FromBody] LauncherHeartbeatRequest m)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var slug = (m.GameSlug ?? string.Empty).Trim().ToLowerInvariant();
            if (slug.Length == 0)
                return BadRequest();
            var ahora = DateTime.UtcNow;
            var abierta = await _db.PlaySessions
                .Where(s => s.UserId == user.Id && s.GameSlug == slug && s.EndedAtUtc == null)
                .OrderByDescending(s => s.StartedAtUtc)
                .FirstOrDefaultAsync();
            if (abierta != null && (ahora - abierta.StartedAtUtc).TotalHours < 6)
            {
                abierta.EndedAtUtc = ahora;
                abierta.Seconds = (long)(ahora - abierta.StartedAtUtc).TotalSeconds;
            }
            else
            {
                if (abierta != null)
                {
                    abierta.EndedAtUtc = ahora;
                    abierta.Seconds = Math.Min((long)(ahora - abierta.StartedAtUtc).TotalSeconds, 21600);
                }
                _db.PlaySessions.Add(new PlaySession
                {
                    UserId = user.Id,
                    GameSlug = slug,
                    GameTitle = (m.GameTitle ?? slug).Trim(),
                    StartedAtUtc = ahora,
                    EndedAtUtc = null,
                    Seconds = 0
                });
            }
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        [HttpPost("session/end")]
        public async Task<IActionResult> EndSession([FromBody] LauncherHeartbeatRequest m)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            var slug = (m.GameSlug ?? string.Empty).Trim().ToLowerInvariant();
            var ahora = DateTime.UtcNow;
            var abiertas = await _db.PlaySessions
                .Where(s => s.UserId == user.Id && s.GameSlug == slug && s.EndedAtUtc == null)
                .ToListAsync();
            foreach (var s in abiertas)
            {
                s.EndedAtUtc = ahora;
                s.Seconds = Math.Max(s.Seconds, (long)(ahora - s.StartedAtUtc).TotalSeconds);
            }
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        private static string NormalizarKey(string? raw)
        {
            var k = (raw ?? string.Empty).Trim().ToUpperInvariant();
            // Acepta con/sin guiones y con espacios: deja solo A-Z/0-9 y reagrupa.
            var alnum = new string(k.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (alnum.Length == 15 && !k.Contains('-'))
                return string.Join("-", new[] { alnum[..5], alnum.Substring(5, 5), alnum.Substring(10, 5) });
            return k.Replace(" ", string.Empty);
        }

        // Catálogo de la STORE para el launcher: cada juego trae su opción de canje.
        // Si viene token válido, cada juego indica si ya está en tu biblioteca (owned).
        [HttpGet("store/games")]
        public async Task<IActionResult> StoreGames()
        {
            var user = await UsuarioPorTokenAsync();
            HashSet<int> ownedIds = new();
            if (user != null)
            {
                ownedIds = _db.StoreOrders
                    .Where(o => (o.RedeemedByUserId == user.Id
                        || (o.BuyerUserId == user.Id && o.Status == "Pagado"))
                        && o.Status == "Pagado")
                    .Select(o => o.GameId)
                    .ToHashSet();
                // También cuenta canjes donde el comprador es otro pero redimió este usuario.
                foreach (var gid in _db.StoreOrders
                    .Where(o => o.RedeemedByUserId == user.Id)
                    .Select(o => o.GameId).ToHashSet())
                    ownedIds.Add(gid);
            }
            var games = _db.StoreGames
                .Where(g => g.IsPublished)
                .OrderByDescending(g => g.Id)
                .Take(100)
                .AsEnumerable()
                .Select(g => new
                {
                    id = g.Id,
                    title = g.Title,
                    description = g.Description,
                    price = g.Price,
                    priceText = "$" + g.Price.ToString("0.##"),
                    coverPath = g.CoverPath,
                    isPreorder = g.IsPreorder,
                    isEarlyAccess = g.IsEarlyAccess,
                    trailerUrl = g.TrailerUrl,
                    owned = ownedIds.Contains(g.Id)
                })
                .ToList();
            return Ok(new { ok = true, games });
        }

        // Valida la key contra el servidor (única, un solo uso).
        // 200 { ok, gameId, gameTitle } → el launcher la añade a la biblioteca.
        // 400 KEY_REQUIRED | KEY_NOT_PAID · 404 KEY_INVALID / GAME_NOT_FOUND · 409 KEY_ALREADY_USED
        [HttpPost("store/redeem")]
        public async Task<IActionResult> Redeem([FromBody] LauncherRedeemRequest m)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized(new { error = "NO_AUTH" });
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var raw = m.KeyCode;
            if (string.IsNullOrWhiteSpace(raw)) raw = m.Key;
            if (string.IsNullOrWhiteSpace(raw)) raw = m.Code;
            var key = NormalizarKey(raw);
            if (string.IsNullOrWhiteSpace(key) || key.Replace("-", string.Empty).Length < 8)
                return BadRequest(new { error = "KEY_REQUIRED" });
            var order = _db.StoreOrders.FirstOrDefault(o => o.KeyCode == key);
            if (order == null)
                return NotFound(new { error = "KEY_INVALID" });
            if (!string.Equals(order.Status, "Pagado", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "KEY_NOT_PAID" });
            if (!string.IsNullOrEmpty(order.RedeemedByUserId) && order.RedeemedByUserId != user.Id)
                return Conflict(new { error = "KEY_ALREADY_USED" });
            var game = _db.StoreGames.FirstOrDefault(g => g.Id == order.GameId);
            if (game == null)
                return NotFound(new { error = "GAME_NOT_FOUND" });
            var ahora = DateTime.UtcNow;
            if (string.IsNullOrEmpty(order.RedeemedByUserId))
            {
                order.RedeemedByUserId = user.Id;
                order.RedeemedAtUtc = ahora;
                await _db.SaveChangesAsync();
            }
            return Ok(new
            {
                ok = true,
                gameId = game.Id,
                gameTitle = game.Title,
                price = game.Price,
                coverPath = game.CoverPath,
                keyCode = order.KeyCode
            });
        }

        // Biblioteca del usuario validada por el servidor (canjes + compras pagadas propias).
        [HttpGet("store/library")]
        public async Task<IActionResult> Library()
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized(new { error = "NO_AUTH" });
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var orders = _db.StoreOrders
                .Where(o => o.Status == "Pagado"
                    && (o.RedeemedByUserId == user.Id || o.BuyerUserId == user.Id))
                .OrderByDescending(o => o.Id)
                .Take(200)
                .ToList();
            var gamesById = _db.StoreGames
                .Where(g => orders.Select(o => o.GameId).Distinct().Contains(g.Id))
                .ToDictionary(g => g.Id);
            var items = new List<object>();
            var vistos = new HashSet<int>();
            foreach (var o in orders)
            {
                if (!vistos.Add(o.GameId)) continue;
                gamesById.TryGetValue(o.GameId, out var g);
                items.Add(new
                {
                    gameId = o.GameId,
                    gameTitle = g?.Title ?? o.GameTitle,
                    price = g?.Price ?? o.Price,
                    coverPath = g?.CoverPath ?? string.Empty,
                    keyCode = o.KeyCode ?? string.Empty,
                    redeemedAtUtc = o.RedeemedAtUtc,
                    orderId = o.Id
                });
            }
            return Ok(new { ok = true, games = items });
        }
    }
}
