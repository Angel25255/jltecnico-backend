namespace JLTecnico.Auth.DTOs
{
    public class AuditLogItem
    {
        public long Id { get; set; }
        public string? NombreUsuario { get; set; }
        public string? CorreoIntento { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string? IP { get; set; }
        public string? UserAgent { get; set; }
        public string? Detalle { get; set; }
        public DateTime FechaHora { get; set; }
    }
}
