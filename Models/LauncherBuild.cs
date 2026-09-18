namespace INNOVATE_INDUSTRIES_WEB_STORE.Models
{
    // Build del launcher subido por el equipo para descargar desde Plataforma.
    public class LauncherBuild
    {
        public int Id { get; set; }

        // Versión visible (ej. v1.0.0).
        public string Version { get; set; } = string.Empty;

        // Descripción larga del launcher.
        public string? Description { get; set; }

        // Ficha técnica: una especificación por línea.
        public string? Specs { get; set; }

        // Ruta pública de la captura (ej. /uploads/launcher/<guid>.png) o null.
        public string? ScreenshotPath { get; set; }

        public string? Notes { get; set; }

        // Ruta pública del archivo (ej. /uploads/launcher/<guid>.zip).
        public string FilePath { get; set; } = string.Empty;

        // Nombre original del archivo subido.
        public string FileName { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public bool IsPublished { get; set; } = true;

        public long Downloads { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string? CreatedByUserId { get; set; }
    }
}
