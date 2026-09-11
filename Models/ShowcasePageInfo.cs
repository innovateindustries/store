namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Categoría SHOWCASE: directo de Twitch embebido.
    // Fuente única: https://www.twitch.tv/tarloox
    public class ShowcasePageInfo
    {
        public string ChannelName { get; set; } = "tarloox";

        public string ChannelUrl { get; set; } = "https://www.twitch.tv/tarloox";

        // Host que sirve la página (localhost en dev, dominio en prod).
        // Twitch exige el parámetro parent exacto para permitir el embed.
        public string ParentHost { get; set; } = "localhost";

        // Estado en vivo (vía API pública, con caché). Si falla, el player
        // de Twitch igual muestra el directo o la pantalla offline solo.
        public bool? IsLive { get; set; }

        public string UptimeText { get; set; } = string.Empty;

        public bool Success { get; set; }

        public DateTime FetchedAtUtc { get; set; }
    }
}
