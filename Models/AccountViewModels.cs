using System.ComponentModel.DataAnnotations;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Email no válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mínimo 6 caracteres.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "El email o usuario es obligatorio.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public class InternalKeyViewModel
    {
        [Required(ErrorMessage = "La key es obligatoria.")]
        public string Key { get; set; } = string.Empty;
    }

    public class InternalRegisterViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Correo no válido.")]
        public string WorkEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los nombres son obligatorios.")]
        public string Nombres { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        public string UserName { get; set; } = string.Empty;

        // La contraseña ES la key de invitación (se fija en el servidor).
        public string Password { get; set; } = string.Empty;
    }

    public class AjustesViewModel
    {
        // Categorías opcionales (desactivadas por defecto).
        public bool Showcase { get; set; }

        public bool Eventos { get; set; }

        public bool Vlog { get; set; }
    }
}
