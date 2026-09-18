namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Sesión de juego reportada por el launcher (horas de juego).
    public class PlaySession
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string GameSlug { get; set; } = string.Empty;

        public string GameTitle { get; set; } = string.Empty;

        public DateTime StartedAtUtc { get; set; }

        public DateTime? EndedAtUtc { get; set; }

        public long Seconds { get; set; }
    }

    // Token de API para el launcher (se guarda el hash, nunca el token).
    public class LauncherToken
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public bool Revoked { get; set; }
    }

    public class LauncherLoginRequest
    {
        public string Identifier { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public class LauncherLoginResponse
    {
        public string Token { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public List<string> Roles { get; set; } = new();
    }

    public class LauncherHeartbeatRequest
    {
        public string GameSlug { get; set; } = string.Empty;

        public string GameTitle { get; set; } = string.Empty;
    }

    public class HorasRow
    {
        public string UserName { get; set; } = string.Empty;

        public string GameTitle { get; set; } = string.Empty;

        public string GameSlug { get; set; } = string.Empty;

        public long TotalSeconds { get; set; }

        public int Sessions { get; set; }

        public DateTime? LastPlayedUtc { get; set; }

        public string TotalHoras => $"{TotalSeconds / 3600}h {(TotalSeconds % 3600) / 60}m";
    }

    public class HorasViewModel
    {
        public List<HorasRow> Filas { get; set; } = new();
    }

    public class LauncherRegisterRequest
    {
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Plan { get; set; } = "USUARIO";
    }

    public class LauncherInternalRegisterRequest
    {
        public string Username { get; set; } = string.Empty;

        public string WorkEmail { get; set; } = string.Empty;

        public string Nombres { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;
    }

    // Canje de key de la STORE desde el launcher.
    // La key es única (XXXXX-XXXXX-XXXXX, un solo uso).
    public class LauncherRedeemRequest
    {
        public string KeyCode { get; set; } = string.Empty;

        // Alias aceptados desde el launcher antiguo.
        public string? Key { get; set; }

        public string? Code { get; set; }
    }
}
