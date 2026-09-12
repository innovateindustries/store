using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
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
    }
}
