namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Metadatos en vivo de la página de itch.io.
    // Única fuente externa de la categoría NUESTRA PLATAFORMA.
    public class ItchPageInfo
    {
        public string PageUrl { get; set; } = "https://innovate-industries.itch.io/innovate-launcher";

        public string Title { get; set; } = "INNOVATE INDUSTRIES PLATFORM";

        public string Description { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        // true si itch.io pide contraseña (el contenido completo no es legible hasta desbloquearla).
        public bool IsPasswordProtected { get; set; }

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime FetchedAtUtc { get; set; }
    }
}
