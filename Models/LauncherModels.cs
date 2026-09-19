using System.ComponentModel.DataAnnotations;

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

    // Key de fundador para registrarse con rol FOUNDER desde el launcher.
    // Solo visible/gestionable por roles CEO y FOUNDER. La validación es
    // 100% servidor: el cliente nunca decide roles.
    public class FounderKey
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string? UsedByUserId { get; set; }

        public DateTime? UsedAtUtc { get; set; }

        public bool IsActive { get; set; } = true;

        // Usos permitidos (1 = un solo uso). UsesCount lleva los consumidos.
        public int MaxUses { get; set; } = 1;

        public int UsesCount { get; set; }

        public DateTime? ExpiresAtUtc { get; set; }
    }

    // Registro fundador desde el launcher: exige FounderKey válida.
    public class LauncherFounderRegisterRequest
    {
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        // Alias del launcher (workEmail/nombres).
        public string? WorkEmail { get; set; }

        public string? Nombres { get; set; }
    }

    // Puntos canjeables del usuario (1 USD = 100 pts al confirmar un pago).
    public class LauncherPoints
    {
        [Key]
        public string UserId { get; set; } = string.Empty;

        public int Points { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }

    // Foto de perfil del launcher (ruta pública en wwwroot/uploads/profiles/).
    public class LauncherProfile
    {
        [Key]
        public string UserId { get; set; } = string.Empty;

        public string ImagePath { get; set; } = string.Empty;

        public DateTime UpdatedAtUtc { get; set; }
    }

    // Build distribuible de un juego (manifiesto + archivos con SHA-256).
    public class GameBuild
    {
        public int Id { get; set; }

        public int GameId { get; set; }

        public string Version { get; set; } = string.Empty;

        public string Exe { get; set; } = string.Empty;

        // Manifiesto JSON (slug, version, builtAt, totalSize, exe, files[]).
        public string ManifestJson { get; set; } = string.Empty;

        public long TotalSize { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string? CreatedByUserId { get; set; }
    }

    public class LauncherCreateOrderRequest
    {
        public int GameId { get; set; }
    }

    public class LauncherConfirmOrderRequest
    {
        public string? Nota { get; set; }
    }

    public class LauncherGrantPointsRequest
    {
        public int Amount { get; set; }
    }

    public class LauncherRedeemRewardRequest
    {
        public string RewardId { get; set; } = string.Empty;
    }

    public class LauncherProfilePhotoRequest
    {
        public string? PhotoB64 { get; set; }
    }

    public class LauncherManifestFileDto
    {
        public string Path { get; set; } = string.Empty;

        public long Size { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }

    public class LauncherPublishBuildRequest
    {
        public string Version { get; set; } = string.Empty;

        public string? Exe { get; set; }

        public List<LauncherManifestFileDto> Files { get; set; } = new();
    }

    // Publicación completa ADMIN → STORE desde el launcher (todo obligatorio).
    public class LauncherPublishGameRequest
    {
        public int? Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string TrailerUrl { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsPreorder { get; set; }

        public bool InPass { get; set; }

        public List<string> Regions { get; set; } = new();

        public string? CoverB64 { get; set; }

        public List<string> ScreenshotsB64 { get; set; } = new();
    }
}
