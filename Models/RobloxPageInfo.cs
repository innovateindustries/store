namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Categoría ROBLOX. Sin link oficial aún: muestra el hub base y el estado
    // de conexión. Cuando se proporcione el juego/grupo, se sincronizará en
    // vivo igual que Plataforma y Eventos (Roblox Games API por universeId).
    public class RobloxPageInfo
    {
        // URL temporal hasta recibir el link oficial del juego o grupo.
        public string PageUrl { get; set; } = "https://www.roblox.com/";

        public bool IsConnected { get; set; } = false;

        public string GameName { get; set; } = "INNOVATE Experience";

        public string GroupName { get; set; } = "INNOVATE INDUSTRIES";
    }
}
