namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Noticia del equipo para que los usuarios sigan el desarrollo.
    public class NewsItem
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        // Ruta pública de la imagen (ej. /uploads/news/abc.png) o null.
        public string? ImagePath { get; set; }

        public string? LinkUrl { get; set; }

        public string? LinkText { get; set; }

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; }

        public string? CreatedByUserId { get; set; }
    }
}
