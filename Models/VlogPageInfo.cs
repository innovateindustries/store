namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Categoría VLOG. Fuente única: https://www.instagram.com/innovateindustriesof/
    // Instagram no expone datos del perfil a bots y bloquea iframes del perfil,
    // así que se verifica la cuenta en vivo y los reels/posts se embeben
    // con el player oficial cuando se proporcionen sus links.
    public class VlogPageInfo
    {
        public string Username { get; set; } = "innovateindustriesof";

        public string ProfileUrl { get; set; } = "https://www.instagram.com/innovateindustriesof/";

        // Widget público de Instagram (permite iframe): muestra el contenido
        // del perfil sin salir de la página.
        public string EmbedUrl { get; set; } = "https://www.instagram.com/innovateindustriesof/embed/";

        public bool IsReachable { get; set; }

        public bool Success { get; set; }

        // Links de reels/posts para embeber (vacío hasta recibirlos).
        public List<string> PostEmbedUrls { get; set; } = new();

        public DateTime FetchedAtUtc { get; set; }
    }
}
