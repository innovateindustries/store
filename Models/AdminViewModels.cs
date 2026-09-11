using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    public class AdminUserRow
    {
        public string Id { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Nombres { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Roles { get; set; } = string.Empty;

        public bool IsBanned { get; set; }

        public bool IsSelf { get; set; }
    }

    public class AdminKeyRow
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string UsedBy { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }

    public class AdminIndexViewModel
    {
        public List<AdminUserRow> Internos { get; set; } = new();

        public List<AdminUserRow> Usuarios { get; set; } = new();

        public List<AdminUserRow> Publishers { get; set; } = new();

        public int TotalUsers => Internos.Count + Usuarios.Count + Publishers.Count;

        public List<AdminKeyRow> Keys { get; set; } = new();

        public bool CanManageKeys { get; set; }

        public string? JustCreatedCode { get; set; }
    }

    public class CrearNoticiaViewModel
    {
        [Required(ErrorMessage = "El título es obligatorio.")]
        [StringLength(120, ErrorMessage = "Máximo 120 caracteres.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "El contenido es obligatorio.")]
        public string Body { get; set; } = string.Empty;

        public IFormFile? Image { get; set; }

        [StringLength(500, ErrorMessage = "Máximo 500 caracteres.")]
        public string? LinkUrl { get; set; }

        [StringLength(120, ErrorMessage = "Máximo 120 caracteres.")]
        public string? LinkText { get; set; }

        public bool IsPublished { get; set; } = true;
    }
}
