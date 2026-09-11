using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Filters
{
    // Bloquea categorías desactivadas por el usuario: redirige a Ajustes.
    // Uso: [CategoryEnabled("showcase")]
    public class CategoryEnabledAttribute : ActionFilterAttribute
    {
        private readonly string _key;

        public CategoryEnabledAttribute(string key) => _key = key;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true || !user.HasClaim("cat_" + _key, "true"))
            {
                context.Result = new RedirectToActionResult("Ajustes", "Account", new { disabled = _key });
            }
        }
    }
}
