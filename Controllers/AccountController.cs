using INNOVATE_INDUSTRIES_WEB_STORE.Data;
using INNOVATE_INDUSTRIES_WEB_STORE.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _users;
        private readonly SignInManager<IdentityUser> _signIn;
        private readonly AppDbContext _db;

        public AccountController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, AppDbContext db)
        {
            _users = users;
            _signIn = signIn;
            _db = db;
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel m, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(m);

            var user = new IdentityUser { UserName = m.Email, Email = m.Email };
            var r = await _users.CreateAsync(user, m.Password);
            if (r.Succeeded)
            {
                // El primer usuario con rol fundador: si aún no existe ningún
                // FOUNDER, quien se registra lo recibe (bootstrap sin claves).
                if ((await _users.GetUsersInRoleAsync("FOUNDER")).Count == 0)
                    await _users.AddToRoleAsync(user, "FOUNDER");

                await _signIn.SignInAsync(user, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            foreach (var e in r.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return View(m);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel m, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(m);

            // Acepta email o nombre de usuario (los internos usan username).
            var user = await _users.FindByEmailAsync(m.Email)
                ?? await _users.FindByNameAsync(m.Email);
            if (user != null)
            {
                var r = await _signIn.CheckPasswordSignInAsync(user, m.Password, lockoutOnFailure: false);
                if (r.Succeeded)
                {
                    await _signIn.SignInAsync(user, m.RememberMe);
                    return RedirectToLocal(returnUrl);
                }
            }

            ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
            return View(m);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToLocal(string? returnUrl) =>
            Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");

        // ---- Ajustes de visibilidad por usuario ----

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Ajustes(string? disabled)
        {
            var user = await _users.GetUserAsync(User);
            if (user is null)
                return Challenge();

            var claims = await _users.GetClaimsAsync(user);
            ViewData["Saved"] = TempData["AjustesSaved"];
            ViewData["Disabled"] = disabled;
            return View(new AjustesViewModel
            {
                Showcase = claims.Any(c => c.Type == "cat_showcase" && c.Value == "true"),
                Eventos = claims.Any(c => c.Type == "cat_eventos" && c.Value == "true"),
                Vlog = claims.Any(c => c.Type == "cat_vlog" && c.Value == "true")
            });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ajustes(AjustesViewModel m)
        {
            var user = await _users.GetUserAsync(User);
            if (user is null)
                return Challenge();

            foreach (var key in new[] { "cat_showcase", "cat_eventos", "cat_vlog" })
            {
                var existing = (await _users.GetClaimsAsync(user)).FirstOrDefault(c => c.Type == key);
                if (existing != null)
                    await _users.RemoveClaimAsync(user, existing);
            }
            if (m.Showcase)
                await _users.AddClaimAsync(user, new Claim("cat_showcase", "true"));
            if (m.Eventos)
                await _users.AddClaimAsync(user, new Claim("cat_eventos", "true"));
            if (m.Vlog)
                await _users.AddClaimAsync(user, new Claim("cat_vlog", "true"));

            await _signIn.RefreshSignInAsync(user);
            TempData["AjustesSaved"] = true;
            return RedirectToAction(nameof(Ajustes));
        }

        // ---- Planes ----

        [HttpGet]
        public IActionResult Plans()
        {
            return View();
        }

        // ---- Registro interno por invitación ----

        [HttpGet]
        public IActionResult InternalKey()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InternalKey(InternalKeyViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            var code = m.Key.Trim().ToUpperInvariant();
            var key = FindKey(code);
            if (key is null)
            {
                ModelState.AddModelError(string.Empty, "Key no válida, ya usada o desactivada.");
                return View(m);
            }

            TempData["InternalKey"] = key.Code;
            return RedirectToAction(nameof(InternalRegister));
        }

        [HttpGet]
        public IActionResult InternalRegister()
        {
            var code = TempData.Peek("InternalKey") as string;
            if (string.IsNullOrEmpty(code) || FindKey(code) is null)
                return RedirectToAction(nameof(InternalKey));

            // La contraseña ES la key.
            return View(new InternalRegisterViewModel { Password = code });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InternalRegister(InternalRegisterViewModel m)
        {
            var code = TempData["InternalKey"] as string;
            var key = string.IsNullOrEmpty(code) ? null : FindKey(code);
            if (key is null || string.IsNullOrEmpty(code))
            {
                ModelState.AddModelError(string.Empty, "Tu key ya no es válida. Pídela de nuevo.");
                return RedirectToAction(nameof(InternalKey));
            }

            if (!ModelState.IsValid)
            {
                TempData["InternalKey"] = code;
                m.Password = code;
                return View(m);
            }

            var user = new IdentityUser { UserName = m.UserName.Trim(), Email = m.WorkEmail.Trim() };
            var r = await _users.CreateAsync(user, code);
            if (!r.Succeeded)
            {
                TempData["InternalKey"] = code;
                foreach (var e in r.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                m.Password = code;
                return View(m);
            }

            await _users.AddClaimAsync(user, new Claim("Nombres", m.Nombres.Trim()));
            await _users.AddToRoleAsync(user, "INTERNO");

            key.UsedByUserId = user.Id;
            key.UsedAtUtc = DateTime.UtcNow;
            key.IsActive = false;
            await _db.SaveChangesAsync();

            await _signIn.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Admin");
        }

        private InviteKey? FindKey(string code)
        {
            var c = code.Trim().ToUpperInvariant();
            return _db.InviteKeys.FirstOrDefault(k => k.Code == c && k.IsActive && k.UsedByUserId == null);
        }
    }
}
