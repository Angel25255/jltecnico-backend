using System.Security.Claims;
using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using JLTecnico.Auth.Models;
using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/tecnicos")]
[Authorize]
public class TecnicosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;
    private readonly AuditoriaService _auditoria;

    public TecnicosController(AppDbContext db, PermisosService permisosService, AuditoriaService auditoria)
    {
        _db = db;
        _permisosService = permisosService;
        _auditoria = auditoria;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    // -----------------------------------------------------------
    // Listado de técnicos con su perfil operativo. Si un técnico
    // todavía no tiene perfil configurado, se le muestra con
    // valores por defecto (Disponible, sin especialidad).
    //
    // "OrdenesActivas" queda en 0 hasta que exista el módulo de
    // Órdenes de Servicio - se conectará automáticamente ahí,
    // sin tener que tocar este controller.
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<List<PerfilTecnicoItem>>> Listar()
    {
        if (!await _permisosService.TienePermiso(RolActual(), "TECNICOS_VER"))
            return Forbid();

        var tecnicos = await _db.Usuarios.Where(u => u.Rol == "Tecnico").ToListAsync();
        var perfiles = await _db.PerfilesTecnicos.ToListAsync();

        // Órdenes activas = las que no están Completada ni Cancelada
        var ordenesActivas = await _db.OrdenesServicio
            .Where(o => o.Estado != "Completada" && o.Estado != "Cancelada" && o.TecnicoUsuarioId != null)
            .GroupBy(o => o.TecnicoUsuarioId!.Value)
            .Select(g => new { TecnicoId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.TecnicoId, g => g.Cantidad);

        var resultado = tecnicos.Select(u =>
        {
            var perfil = perfiles.FirstOrDefault(p => p.UsuarioId == u.Id);
            return new PerfilTecnicoItem
            {
                UsuarioId = u.Id,
                NombreCompleto = u.NombreCompleto,
                Correo = u.Correo,
                Activo = u.Activo,
                Especialidad = perfil?.Especialidad,
                EstadoDisponibilidad = perfil?.EstadoDisponibilidad ?? "Disponible",
                CalificacionPromedio = perfil?.CalificacionPromedio,
                TotalServiciosCompletados = perfil?.TotalServiciosCompletados ?? 0,
                OrdenesActivas = ordenesActivas.TryGetValue(u.Id, out var cantidad) ? cantidad : 0
            };
        })
        .OrderBy(t => t.NombreCompleto)
        .ToList();

        return Ok(resultado);
    }

    // -----------------------------------------------------------
    // Crear o actualizar el perfil operativo de un técnico
    // (especialidad, disponibilidad, calificación).
    // Solo el Administrador puede editar esto.
    // -----------------------------------------------------------
    [HttpPut("{usuarioId}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> ActualizarPerfil(int usuarioId, ActualizarPerfilTecnicoRequest request)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario == null || usuario.Rol != "Tecnico")
            return BadRequest(new { mensaje = "El usuario indicado no es un técnico válido." });

        if (request.EstadoDisponibilidad != "Disponible" &&
            request.EstadoDisponibilidad != "Ocupado" &&
            request.EstadoDisponibilidad != "Ausente")
            return BadRequest(new { mensaje = "Estado de disponibilidad inválido." });

        var perfil = await _db.PerfilesTecnicos.FirstOrDefaultAsync(p => p.UsuarioId == usuarioId);

        if (perfil == null)
        {
            perfil = new PerfilTecnico { UsuarioId = usuarioId };
            _db.PerfilesTecnicos.Add(perfil);
        }

        perfil.Especialidad = request.Especialidad;
        perfil.EstadoDisponibilidad = request.EstadoDisponibilidad;
        perfil.CalificacionPromedio = request.CalificacionPromedio;
        perfil.TotalServiciosCompletados = request.TotalServiciosCompletados;

        await _auditoria.Registrar("PERFIL_TECNICO_ACTUALIZADO", usuarioId, null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Técnico #{usuarioId} - Estado: {request.EstadoDisponibilidad}");

        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Perfil del técnico actualizado." });
    }
}