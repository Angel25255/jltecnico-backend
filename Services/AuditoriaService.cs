using JLTecnico.Auth.Data;
using JLTecnico.Auth.Models;

namespace JLTecnico.Auth.Services;

public class AuditoriaService
{
    private readonly AppDbContext _db;

    public AuditoriaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task Registrar(string accion, int? usuarioId, string? correoIntento,
        string ip, string? userAgent, string? detalle = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Accion = accion,
            UsuarioId = usuarioId,
            CorreoIntento = correoIntento,
            IP = ip,
            UserAgent = userAgent,
            Detalle = detalle,
            FechaHora = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
