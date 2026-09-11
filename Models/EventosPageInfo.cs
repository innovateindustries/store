namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    public class EventoInfo
    {
        public string Tag { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool HasCountdown { get; set; }

        public string StatusText { get; set; } = string.Empty;
    }

    // Sincronización en vivo con https://webbun.github.io/innovate-industrie/
    // Única fuente externa de la categoría EVENTOS.
    public class EventosPageInfo
    {
        public string PageUrl { get; set; } = "https://webbun.github.io/innovate-industrie/";

        public string Title { get; set; } = "Eventos";

        public string SectionTitle { get; set; } = string.Empty;

        public string SectionDesc { get; set; } = string.Empty;

        public List<EventoInfo> Events { get; set; } = new();

        // Fecha del próximo evento con cuenta regresiva (UHC), si la web la publica.
        public DateTimeOffset? NextEventDate { get; set; }

        public string LauncherVersion { get; set; } = string.Empty;

        public string LauncherName { get; set; } = string.Empty;

        public string LauncherDesc { get; set; } = string.Empty;

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime FetchedAtUtc { get; set; }
    }
}
