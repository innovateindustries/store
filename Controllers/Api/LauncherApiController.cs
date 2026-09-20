using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Controllers.Api
{
    // API JSON para el launcher de escritorio (tokens, no cookies).
    [ApiController]
    [Route("api/launcher")]
    public class LauncherApiController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public LauncherApiController(UserManager<IdentityUser> users, AppDbContext db, IWebHostEnvironment env)
        {
            _users = users;
            _db = db;
            _env = env;
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

        // Registro fundador desde el launcher: exige FounderKey válida.
        // El rol FOUNDER lo decide el servidor (nunca el cliente).
        [HttpPost("register-founder")]
        public async Task<IActionResult> RegisterFounder([FromBody] LauncherFounderRegisterRequest m)
        {
            var username = (m.Username ?? string.Empty).Trim();
            var email = (m.WorkEmail ?? m.Email ?? string.Empty).Trim();
            var pass = m.Password ?? string.Empty;
            var code = (m.Key ?? string.Empty).Trim().ToUpperInvariant();

            if (username.Length < 3 || username.Any(char.IsWhiteSpace))
                return BadRequest(new { errors = new[] { "Usuario inválido (mínimo 3 caracteres, sin espacios)." } });
            if (!new EmailAddressAttribute().IsValid(email))
                return BadRequest(new { errors = new[] { "Email no válido." } });
            if (pass.Length < 6)
                return BadRequest(new { errors = new[] { "La clave debe tener mínimo 6 caracteres." } });
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { errors = new[] { "Founder Key requerida." } });

            var key = _db.FounderKeys.FirstOrDefault(k => k.Code == code);
            if (key is null || !key.IsActive)
                return BadRequest(new { errors = new[] { "Founder Key no válida o desactivada." } });
            if (key.ExpiresAtUtc != null && key.ExpiresAtUtc < DateTime.UtcNow)
                return BadRequest(new { errors = new[] { "Founder Key expirada." } });
            if (key.UsesCount >= Math.Max(1, key.MaxUses))
                return BadRequest(new { errors = new[] { "Founder Key sin usos restantes." } });

            if (await _users.FindByNameAsync(username) != null)
                return BadRequest(new { errors = new[] { "Ese usuario ya existe en la web." } });
            if (await _users.FindByEmailAsync(email) != null)
                return BadRequest(new { errors = new[] { "Ese email ya está registrado en la web." } });

            var user = new IdentityUser { UserName = username, Email = email };
            var r = await _users.CreateAsync(user, pass);
            if (!r.Succeeded)
                return BadRequest(new { errors = r.Errors.Select(e => e.Description).ToArray() });

            await _users.AddToRoleAsync(user, "FOUNDER");
            var nombres = (m.Nombres ?? string.Empty).Trim();
            if (nombres.Length >= 2)
                await _users.AddClaimAsync(user, new Claim("Nombres", nombres));

            key.UsesCount++;
            if (key.UsesCount >= Math.Max(1, key.MaxUses))
            {
                key.IsActive = false;
                key.UsedByUserId = user.Id;
                key.UsedAtUtc = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();

            return StatusCode(201, new { userName = user.UserName });
        }
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
                    priceText = "$" + g.PrecioEfectivo.ToString("0.##"),
                    salePrice = g.EnOferta ? g.SalePrice : null,
                    offerCoverPath = g.OfferCoverPath ?? string.Empty,
                    onSale = g.EnOferta,
                    coverPath = g.CoverPath,
                    isPreorder = g.IsPreorder,
                    isEarlyAccess = g.IsEarlyAccess,
                    genre = g.Genre,
                    trailerUrl = g.TrailerUrl,
                    screenshots = string.Join("|", ScreenshotsDe(g)),
                    regions = g.Regions,
                    inPass = g.InPass,
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

        // ---------- Fase 2: el backend canónico (Web Store) ----------

        private async Task<bool> EsStaffAsync(IdentityUser u)
        {
            var roles = await _users.GetRolesAsync(u);
            return roles.Contains("CEO") || roles.Contains("FOUNDER") || roles.Contains("INTERNO");
        }

        private static List<string> ScreenshotsDe(StoreGame g)
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(g.ScreenshotsJson ?? "[]") ?? new();
            }
            catch { return new(); }
        }

        private static List<string> ScreenshotsBuild(LauncherBuild b)
        {
            try
            {
                var lista = JsonSerializer.Deserialize<List<string>>(b.ScreenshotsJson ?? "[]") ?? new();
                if (lista.Count == 0 && !string.IsNullOrWhiteSpace(b.ScreenshotPath))
                    lista.Add(b.ScreenshotPath);
                return lista;
            }
            catch { return new(); }
        }

        private async Task<int> PuntosDeAsync(string userId)
        {
            var row = await _db.LauncherPoints.FirstOrDefaultAsync(p => p.UserId == userId);
            return row?.Points ?? 0;
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

        private static object OrdenDto(StoreOrder o, string gameTitle) => new
        {
            reference = o.Reference,
            gameId = o.GameId,
            gameTitle,
            amount = o.Price,
            amountText = o.Price <= 0 ? "FREE" : "$" + o.Price.ToString("0.##"),
            status = o.Status,
            discordUser = (string?)null,
            nota = (string?)null,
            createdAt = o.CreatedAtUtc,
            updatedAt = o.CreatedAtUtc
        };

        // Crear pedido de pago manual (reutiliza el Pendiente del mismo juego).
        [HttpPost("store/order")]
        public async Task<IActionResult> CrearOrden([FromBody] LauncherCreateOrderRequest m)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized(new { error = "NO_AUTH" });
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var game = _db.StoreGames.FirstOrDefault(g => g.Id == m.GameId && g.IsPublished);
            if (game == null)
                return BadRequest(new { error = "GAME_NOT_FOUND" });
            if (game.IsPreorder)
                return BadRequest(new { error = "NOT_FOR_SALE" });
            var existente = _db.StoreOrders.FirstOrDefault(o =>
                o.BuyerUserId == user.Id && o.GameId == game.Id && o.Status == "Pendiente");
            if (existente != null)
                return Ok(OrdenDto(existente, game.Title));
            string reference;
            do
            {
                reference = "ORD-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            } while (_db.StoreOrders.Any(o => o.Reference == reference));
            var orden = new StoreOrder
            {
                GameId = game.Id,
                GameTitle = game.Title,
                Price = game.PrecioEfectivo,
                Reference = reference,
                Status = "Pendiente",
                CreatedAtUtc = DateTime.UtcNow,
                BuyerUserId = user.Id
            };
            _db.StoreOrders.Add(orden);
            await _db.SaveChangesAsync();
            return Ok(OrdenDto(orden, game.Title));
        }

        [HttpGet("store/orders")]
        public async Task<IActionResult> MisOrdenes()
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized(new { error = "NO_AUTH" });
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var ordenes = _db.StoreOrders
                .Where(o => o.BuyerUserId == user.Id)
                .OrderByDescending(o => o.CreatedAtUtc)
                .Take(200)
                .ToList();
            return Ok(new { orders = ordenes.ConvertAll(o => OrdenDto(o, o.GameTitle)) });
        }

        [HttpGet("store/order/{reference}")]
        public async Task<IActionResult> VerOrden(string reference)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized(new { error = "NO_AUTH" });
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var orden = _db.StoreOrders.FirstOrDefault(o =>
                o.Reference == reference && o.BuyerUserId == user.Id);
            if (orden == null)
                return NotFound(new { error = "ORDER_NOT_FOUND" });
            return Ok(OrdenDto(orden, orden.GameTitle));
        }

        // ---------- Puntos y premios (1 USD = 100 pts) ----------

        private sealed record Premio(string Id, string Title, string Description, int Cost, string ValueText, string Icon);

        private static readonly List<Premio> CatalogoPremios = new()
        {
            new("coin_9", "9 USD Digital Coins", "Monedas digitales valor 9 USD", 900, "9 USD", "🪙"),
            new("coin_25", "25 USD Digital Coins", "Monedas digitales valor 25 USD", 2500, "25 USD", "💰"),
            new("game_30", "Juego Premium (30 USD)", "Canjea por cualquier juego hasta 30 USD", 3000, "30 USD", "🎮"),
            new("coin_50", "50 USD Digital Coins", "Monedas digitales valor 50 USD", 5000, "50 USD", "💎"),
            new("coin_100", "100 USD Digital Coins", "Monedas digitales valor 100 USD", 10000, "100 USD", "👑"),
        };

        [HttpGet("points")]
        public async Task<IActionResult> MisPuntos()
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            return Ok(new { points = await PuntosDeAsync(user.Id), userName = user.UserName });
        }

        [HttpGet("rewards")]
        public async Task<IActionResult> Premios()
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var pts = await PuntosDeAsync(user.Id);
            return Ok(new
            {
                points = pts,
                rewards = CatalogoPremios.ConvertAll(r => new
                {
                    id = r.Id, title = r.Title, description = r.Description,
                    cost = r.Cost, valueText = r.ValueText, icon = r.Icon,
                    canAfford = pts >= r.Cost
                })
            });
        }

        [HttpPost("rewards/redeem")]
        public async Task<IActionResult> CanjearPremio([FromBody] LauncherRedeemRewardRequest m)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var premio = CatalogoPremios.FirstOrDefault(r => r.Id == (m.RewardId ?? string.Empty).Trim());
            if (premio == null)
                return NotFound(new { error = "REWARD_NOT_FOUND" });
            var pts = await PuntosDeAsync(user.Id);
            if (pts < premio.Cost)
                return StatusCode(402, new { error = "NOT_ENOUGH_POINTS" });
            await SumarPuntosAsync(user.Id, -premio.Cost);
            await _db.SaveChangesAsync();
            return Ok(new { ok = true, points = pts - premio.Cost, reward = premio.Id, title = premio.Title });
        }

        // ---------- Perfil ----------

        private string UrlPerfil(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return string.Empty;
            if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return imagePath;
            return $"{Request.Scheme}://{Request.Host}" + imagePath;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> MiPerfil()
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var perfil = await _db.LauncherProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            return Ok(new
            {
                userName = user.UserName,
                profileImagePath = perfil?.ImagePath ?? string.Empty,
                profileImageUrl = UrlPerfil(perfil?.ImagePath),
                points = await PuntosDeAsync(user.Id)
            });
        }

        [HttpPost("profile/photo")]
        public async Task<IActionResult> SubirFoto([FromBody] LauncherProfilePhotoRequest m)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var b64 = (m.PhotoB64 ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(b64) || b64.Length < 60)
                return BadRequest(new { error = "PHOTO_REQUIRED" });
            if (b64.Length > 2_800_000)
                return BadRequest(new { error = "PHOTO_TOO_LARGE" });
            byte[] bytes;
            try
            {
                var clean = b64.Contains(',') ? b64[(b64.IndexOf(',') + 1)..] : b64;
                bytes = Convert.FromBase64String(clean);
            }
            catch { return BadRequest(new { error = "PHOTO_INVALID" }); }
            if (bytes.Length < 16)
                return BadRequest(new { error = "PHOTO_INVALID" });
            var dir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(dir);
            var nombre = "u-" + user.Id + ".png";
            await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, nombre), bytes);
            var ruta = "/uploads/profiles/" + nombre;
            var perfil = await _db.LauncherProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (perfil == null)
            {
                perfil = new LauncherProfile { UserId = user.Id, UpdatedAtUtc = DateTime.UtcNow };
                _db.LauncherProfiles.Add(perfil);
            }
            perfil.ImagePath = ruta;
            perfil.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { ok = true, profileImagePath = ruta, profileImageUrl = UrlPerfil(ruta) });
        }

        [HttpDelete("profile/photo")]
        public async Task<IActionResult> BorrarFoto()
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var perfil = await _db.LauncherProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (perfil != null && !string.IsNullOrWhiteSpace(perfil.ImagePath))
            {
                try
                {
                    var full = Path.Combine(_env.WebRootPath,
                        perfil.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                }
                catch { /* mejor esfuerzo */ }
                _db.LauncherProfiles.Remove(perfil);
                await _db.SaveChangesAsync();
            }
            return Ok(new { ok = true });
        }

        // ---------- Distribución de juegos por manifiesto ----------

        private async Task<bool> ConLicenciaAsync(IdentityUser user, int gameId)
        {
            if (await EsStaffAsync(user)) return true;
            return _db.StoreOrders.Any(o => o.GameId == gameId
                && o.Status == "Pagado"
                && (o.RedeemedByUserId == user.Id || o.BuyerUserId == user.Id));
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

        [HttpGet("games/{id:int}/manifest")]
        public async Task<IActionResult> ManifiestoJuego(int id)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var game = _db.StoreGames.FirstOrDefault(g => g.Id == id && g.IsPublished);
            if (game == null)
                return NotFound(new { error = "GAME_NOT_FOUND" });
            if (!await ConLicenciaAsync(user, id))
                return StatusCode(402, new { error = "NOT_OWNED" });
            var build = _db.GameBuilds.FirstOrDefault(b => b.GameId == id);
            if (build == null || string.IsNullOrWhiteSpace(build.ManifestJson))
                return NotFound(new { error = "NO_MANIFEST" });
            return Content(build.ManifestJson, "application/json");
        }

        [HttpGet("games/{id:int}/file")]
        public async Task<IActionResult> ArchivoJuego(int id)
        {
            var user = await UsuarioPorTokenAsync();
            if (user == null)
                return Unauthorized();
            if (Baneado(user))
                return StatusCode(403, new { banned = true });
            var game = _db.StoreGames.FirstOrDefault(g => g.Id == id && g.IsPublished);
            if (game == null)
                return NotFound(new { error = "GAME_NOT_FOUND" });
            if (!await ConLicenciaAsync(user, id))
                return StatusCode(402, new { error = "NOT_OWNED" });
            var build = _db.GameBuilds.FirstOrDefault(b => b.GameId == id);
            var v = (Request.Query["v"].ToString() ?? string.Empty).Trim();
            var rel = (Request.Query["path"].ToString() ?? string.Empty).Trim();
            if (build == null || string.IsNullOrWhiteSpace(v) || string.IsNullOrWhiteSpace(rel)
                || !string.Equals(build.Version, v, StringComparison.Ordinal))
                return BadRequest();
            var raiz = Path.Combine(_env.WebRootPath, "uploads", "builds", id.ToString(), v);
            var full = RutaSegura(raiz, rel);
            if (full == null || !System.IO.File.Exists(full))
                return NotFound();
            return PhysicalFile(full, "application/octet-stream", enableRangeProcessing: true);
        }

        // Noticias publicadas (vitrina pública, sin auth).
        [HttpGet("/api/news/public")]
        public IActionResult NoticiasPublicas()
        {
            var items = _db.NewsItems
                .Where(n => n.IsPublished)
                .OrderByDescending(n => n.Id)
                .Take(20)
                .ToList()
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    body = n.Body,
                    imagePath = n.ImagePath ?? string.Empty,
                    linkUrl = n.LinkUrl ?? string.Empty,
                    linkText = n.LinkText ?? string.Empty,
                    createdAtUtc = n.CreatedAtUtc
                })
                .ToList();
            return Ok(new { news = items });
        }

        // Builds publicados del launcher (vitrina pública, sin auth).
        [HttpGet("/api/builds/public")]
        public IActionResult BuildsPublicos()
        {
            var builds = _db.LauncherBuilds
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.Id)
                .Take(10)
                .ToList()
                .Select(b => new
                {
                    id = b.Id,
                    version = b.Version,
                    description = b.Description ?? string.Empty,
                    specs = b.Specs ?? string.Empty,
                    screenshotPath = b.ScreenshotPath ?? string.Empty,
                    screenshots = ScreenshotsBuild(b),
                    notes = b.Notes ?? string.Empty,
                    filePath = b.FilePath,
                    fileName = b.FileName,
                    sizeBytes = b.SizeBytes,
                    isMandatory = b.IsMandatory,
                    downloads = b.Downloads,
                    createdAtUtc = b.CreatedAtUtc
                })
                .ToList();
            return Ok(new { builds });
        }

        // ---------- Manifiesto del propio launcher (auto-update) ----------
        [HttpGet("client/manifest")]
        public IActionResult ManifiestoCliente()
        {
            var build = _db.LauncherBuilds
                .Where(b => b.IsPublished)
                .OrderByDescending(b => b.Id)
                .FirstOrDefault();
            if (build == null)
                return NotFound(new { error = "NO_CLIENT_BUILD" });
            return Ok(new
            {
                version = build.Version,
                download = build.FilePath,
                fileName = build.FileName,
                sizeBytes = build.SizeBytes,
                releaseNotes = build.Notes ?? build.Description ?? string.Empty,
                mandatory = build.IsMandatory
            });
        }
    }
}
