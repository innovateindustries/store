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

        // Solo founder keys: "usos/max" (vacío en keys de invitación).
        public string UsesInfo { get; set; } = string.Empty;
    }

    public class AdminIndexViewModel
    {
        public List<AdminUserRow> Internos { get; set; } = new();

        public List<AdminUserRow> Usuarios { get; set; } = new();

        public List<AdminUserRow> Publishers { get; set; } = new();

        public int TotalUsers => Internos.Count + Usuarios.Count + Publishers.Count;

        public List<AdminKeyRow> Keys { get; set; } = new();

        public List<AdminKeyRow> FounderKeys { get; set; } = new();

        public bool CanManageKeys { get; set; }

        public string? JustCreatedCode { get; set; }

        public string? JustCreatedFounderCode { get; set; }
    }

    public class CrearNoticiaViewModel
    {        [Required(ErrorMessage = "El título es obligatorio.")]
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

    public class SubirLauncherViewModel
    {
        [Required(ErrorMessage = "La versión es obligatoria (ej. v1.0.0).")]
        [StringLength(32, ErrorMessage = "Máximo 32 caracteres.")]
        public string Version { get; set; } = string.Empty;

        [StringLength(4000, ErrorMessage = "Máximo 4000 caracteres.")]
        public string? Description { get; set; }

        [StringLength(2000, ErrorMessage = "Máximo 2000 caracteres.")]
        public string? Specs { get; set; }

        [StringLength(2000, ErrorMessage = "Máximo 2000 caracteres.")]
        public string? Notes { get; set; }

        public IFormFile? Archivo { get; set; }

        // Capturas del launcher (máx. 4, ≤5 MB c/u). La primera es la principal.
        public List<IFormFile> Screenshots { get; set; } = new();

        public bool IsPublished { get; set; } = true;

        // Actualización obligatoria (el launcher debe instalarla).
        public bool IsMandatory { get; set; }
    }

    // Kit de publicación de la STORE: todos los campos son obligatorios
    // salvo los dos checks (responder No/No = lanzamiento normal) y el
    // flag de publicado. Preventa y Acceso anticipado son excluyentes.
    public class PublicarStoreViewModel
    {
        [Required(ErrorMessage = "El título es obligatorio.")]
        [StringLength(120, ErrorMessage = "Máximo 120 caracteres.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(4000, ErrorMessage = "Máximo 4000 caracteres.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los requisitos mínimos son obligatorios.")]
        [StringLength(2000, ErrorMessage = "Máximo 2000 caracteres.")]
        public string MinRequirements { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los requisitos recomendados son obligatorios.")]
        [StringLength(2000, ErrorMessage = "Máximo 2000 caracteres.")]
        public string RecRequirements { get; set; } = string.Empty;

        public bool IsPreorder { get; set; }

        public bool IsEarlyAccess { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(0, 999999, ErrorMessage = "Precio no válido.")]
        public decimal? Price { get; set; }

        // Portada (carátula). Obligatoria.
        public IFormFile? Cover { get; set; }

        // Screenshots: de 1 a 6. Obligatorios.
        public List<IFormFile>? Screenshots { get; set; }

        [Required(ErrorMessage = "El tráiler es obligatorio (URL de YouTube o MP4).")]
        [StringLength(500, ErrorMessage = "Máximo 500 caracteres.")]
        public string TrailerUrl { get; set; } = string.Empty;

        public bool IsPublished { get; set; } = true;
    }

    // USUARIOS · Biblioteca: un usuario del launcher/web con el conteo de
    // juegos canjeados distintos y de keys entregadas.
    public class AdminLibraryUserRow
    {
        public string Id { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Roles { get; set; } = string.Empty;

        public int GamesCount { get; set; }

        public int KeysCount { get; set; }
    }

    public class AdminUsuariosViewModel
    {
        public List<AdminLibraryUserRow> Users { get; set; } = new();
    }

    // Detalle de biblioteca: pedidos del usuario con la key usada en cada uno.
    public class AdminUsuarioDetalleViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Roles { get; set; } = string.Empty;

        public List<StoreOrder> Orders { get; set; } = new();
    }
}
