using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = "Administrador")]
public class AuditoriaController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditoriaController(AppDbContext db)
    {
        _db = db;
    }

    // -----------------------------------------------------------
    // GET /api/auditoria?desde=2026-07-01&hasta=2026-07-18&accion=LOGIN_OK&correo=x&pagina=1&tamano=50
    // Todos los filtros son opcionales.
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<AuditLogPaginadoResponse>> Obtener(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? accion,
        [FromQuery] string? correo,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 50)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1 || tamano > 200) tamano = 50;

        var query = _db.AuditLogs.AsQueryable();

        if (desde.HasValue)
            query = query.Where(a => a.FechaHora >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(a => a.FechaHora <= hasta.Value.AddDays(1).AddTicks(-1));

        if (!string.IsNullOrWhiteSpace(accion))
            query = query.Where(a => a.Accion == accion);

        if (!string.IsNullOrWhiteSpace(correo))
            query = query.Where(a => a.CorreoIntento != null && a.CorreoIntento.Contains(correo));

        int total = await query.CountAsync();

        var logs = await query
            .OrderByDescending(a => a.FechaHora)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync();

        // Traemos los nombres de usuario en un segundo query simple (evita problemas de navegación)
        var usuarioIds = logs.Where(l => l.UsuarioId.HasValue).Select(l => l.UsuarioId!.Value).Distinct().ToList();
        var nombres = await _db.Usuarios
            .Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto);

        var items = logs.Select(l => new AuditLogItem
        {
            Id = l.Id,
            NombreUsuario = l.UsuarioId.HasValue && nombres.ContainsKey(l.UsuarioId.Value)
                ? nombres[l.UsuarioId.Value]
                : null,
            CorreoIntento = l.CorreoIntento,
            Accion = l.Accion,
            IP = l.IP,
            UserAgent = l.UserAgent,
            Detalle = l.Detalle,
            FechaHora = l.FechaHora
        }).ToList();

        return Ok(new AuditLogPaginadoResponse
        {
            Items = items,
            TotalRegistros = total,
            Pagina = pagina,
            TamanoPagina = tamano
        });
    }

    // -----------------------------------------------------------
    // Lista de tipos de acción distintos, para llenar el filtro
    // desplegable en el frontend sin hardcodearlo.
    // -----------------------------------------------------------
    [HttpGet("tipos-accion")]
    public async Task<ActionResult<List<string>>> TiposAccion()
    {
        var tipos = await _db.AuditLogs.Select(a => a.Accion).Distinct().OrderBy(a => a).ToListAsync();
        return Ok(tipos);
    }
}