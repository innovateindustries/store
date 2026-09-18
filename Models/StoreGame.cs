namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Juego publicado en la STORE (kit de publicación del Admin).
    public class StoreGame
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // Requisitos mínimos (texto libre, uno por línea).
        public string MinRequirements { get; set; } = string.Empty;

        // Requisitos recomendados (texto libre, uno por línea).
        public string RecRequirements { get; set; } = string.Empty;

        // ¿Es preventa (aún no sale a la venta)?
        public bool IsPreorder { get; set; }

        // ¿Es ACCESO ANTICIPADO (Early Access jugable)?
        public bool IsEarlyAccess { get; set; }

        public decimal Price { get; set; }

        // Ruta pública de la portada (carátula, ej. /uploads/store/<guid>.png).
        public string CoverPath { get; set; } = string.Empty;

        // JSON con la lista de rutas de screenshots (máx. 6).
        public string ScreenshotsJson { get; set; } = "[]";

        // Tráiler: URL de YouTube o MP4 directo.
        public string TrailerUrl { get; set; } = string.Empty;

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; }

        public string? CreatedByUserId { get; set; }
    }
}
