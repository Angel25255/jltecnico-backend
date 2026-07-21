namespace JLTecnico.Auth.Models;

public class AuditLog
{
    public long Id { get; set; }
    public int? UsuarioId { get; set; }
    public string? CorreoIntento { get; set; }

    // LOGIN_OK, LOGIN_FALLIDO, 2FA_FALLIDO, USUARIO_DESACTIVADO_INTENTO,
    // SESION_CERRADA, CUENTA_BLOQUEADA, DISPOSITIVO_NUEVO
    public string Accion { get; set; } = string.Empty;

    public string? IP { get; set; }
    public string? UserAgent { get; set; }
    public string? Detalle { get; set; }

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
}
