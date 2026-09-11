namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Key de invitación para registrarse como TRABAJADOR INTERNO.
    // Solo visible/gestionable por roles CEO y FOUNDER.
    public class InviteKey
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string? UsedByUserId { get; set; }

        public DateTime? UsedAtUtc { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
