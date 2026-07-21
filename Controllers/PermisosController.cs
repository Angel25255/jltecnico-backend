using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/permisos")]
[Authorize]
public class PermisosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;
    private readonly AuditoriaService _auditoria;

    public PermisosController(AppDbContext db, PermisosService permisosService, AuditoriaService auditoria)
    {
        _db = db;
        _permisosService = permisosService;
        _auditoria = auditoria;
    }

    // -----------------------------------------------------------
    // Permisos habilitados para el rol del usuario que hizo login.
    // El frontend llama esto justo después de entrar, para saber
    // qué módulos/botones mostrar.
    // -----------------------------------------------------------
    [HttpGet("mis-permisos")]
    public async Task<ActionResult<List<string>>> MisPermisos()
    {
        string rol = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        var claves = await _permisosService.ObtenerClavesPermitidas(rol);
        return Ok(claves);
    }

    // -----------------------------------------------------------
    // Matriz completa: todos los permisos, con columnas
    // Vendedor / Tecnico (Administrador no se muestra, siempre
    // tiene todo). Solo el Administrador puede ver/editar esto.
    // -----------------------------------------------------------
    [HttpGet("matriz")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<List<PermisoMatrizItem>>> ObtenerMatriz()
    {
        var permisos = await _db.Permisos.OrderBy(p => p.Modulo).ThenBy(p => p.Clave).ToListAsync();
        var rolPermisos = await _db.RolPermisos.ToListAsync();

        var resultado = permisos.Select(p => new PermisoMatrizItem
        {
            PermisoId = p.Id,
            Clave = p.Clave,
            Modulo = p.Modulo,
            Descripcion = p.Descripcion,
            VendedorPermitido = rolPermisos.Any(rp => rp.Rol == "Vendedor" && rp.PermisoId == p.Id && rp.Permitido),
            TecnicoPermitido = rolPermisos.Any(rp => rp.Rol == "Tecnico" && rp.PermisoId == p.Id && rp.Permitido),
        }).ToList();

        return Ok(resultado);
    }

    // -----------------------------------------------------------
    // Activar/desactivar UN permiso para UN rol (Vendedor o Tecnico)
    // -----------------------------------------------------------
    [HttpPut("matriz")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> ActualizarPermiso(ActualizarPermisoRequest request)
    {
        if (request.Rol != "Vendedor" && request.Rol != "Tecnico")
            return BadRequest(new { mensaje = "Solo se pueden configurar permisos para Vendedor o Tecnico." });

        var rolPermiso = await _db.RolPermisos
            .FirstOrDefaultAsync(rp => rp.Rol == request.Rol && rp.PermisoId == request.PermisoId);

        if (rolPermiso == null)
        {
            rolPermiso = new Models.RolPermiso
            {
                Rol = request.Rol,
                PermisoId = request.PermisoId,
                Permitido = request.Permitido
            };
            _db.RolPermisos.Add(rolPermiso);
        }
        else
        {
            rolPermiso.Permitido = request.Permitido;
        }

        var permiso = await _db.Permisos.FindAsync(request.PermisoId);

        await _auditoria.Registrar(
            request.Permitido ? "PERMISO_OTORGADO" : "PERMISO_REVOCADO",
            null, null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Rol: {request.Rol}, Permiso: {permiso?.Clave}");

        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Permiso actualizado." });
    }
}