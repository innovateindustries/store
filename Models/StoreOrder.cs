namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Pedido de pago manual de un juego de la STORE.
    // Sin pasarela conectada: se registra como Pendiente con referencia
    // y el equipo lo valida (Admin → STORE → Pedidos).
    public class StoreOrder
    {
        public int Id { get; set; }

        public int GameId { get; set; }

        public string GameTitle { get; set; } = string.Empty;

        public decimal Price { get; set; }

        // Referencia única (ej. ORD-9F3A2B1C) para el pago manual.
        public string Reference { get; set; } = string.Empty;

        // Key del juego (ej. K7Q2M-9ZX4P-3BD8W): 15 caracteres en 3 grupos
        // de 5 + 2 guiones = 17 caracteres. Única. Se genera cuando el
        // equipo marca el pedido como Pagado; null mientras está Pendiente.
        public string? KeyCode { get; set; }

        // Pendiente | Pagado
        public string Status { get; set; } = "Pendiente";

        public DateTime CreatedAtUtc { get; set; }

        public string? BuyerUserId { get; set; }

        // Canje en el launcher: la key es única (un solo uso).
        // Quién la canjeó (UserId de Identity) y cuándo. Null = sin canjear.
        public string? RedeemedByUserId { get; set; }

        public DateTime? RedeemedAtUtc { get; set; }
    }
}
