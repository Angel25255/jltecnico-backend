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
[Route("api/proveedores")]
[Authorize]
public class ProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;
    private readonly AuditoriaService _auditoria;

    public ProveedoresController(AppDbContext db, PermisosService permisosService, AuditoriaService auditoria)
    {
        _db = db;
        _permisosService = permisosService;
        _auditoria = auditoria;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    [HttpGet]
    public async Task<ActionResult<List<ProveedorItem>>> Listar([FromQuery] string? busqueda)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "INVENTARIO_VER"))
            return Forbid();

        var query = _db.Proveedores.AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            query = query.Where(p =>
                p.RazonSocial.Contains(busqueda) ||
                p.Ruc.Contains(busqueda));
        }

        var proveedores = await query
            .OrderByDescending(p => p.FechaCreacion)
            .Select(p => new ProveedorItem
            {
                Id = p.Id,
                Ruc = p.Ruc,
                RazonSocial = p.RazonSocial,
                NombreContacto = p.NombreContacto,
                Telefono = p.Telefono,
                Correo = p.Correo,
                Direccion = p.Direccion,
                Activo = p.Activo,
                FechaCreacion = p.FechaCreacion
            })
            .ToListAsync();

        return Ok(proveedores);
    }

    [HttpPost]
    public async Task<ActionResult<ProveedorItem>> Crear(CrearProveedorRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "INVENTARIO_GESTIONAR"))
            return Forbid();

        bool existe = await _db.Proveedores.AnyAsync(p => p.Ruc == request.Ruc);
        if (existe)
            return BadRequest(new { mensaje = "Ya existe un proveedor con ese RUC." });

        var proveedor = new Proveedor
        {
            Ruc = request.Ruc,
            RazonSocial = request.RazonSocial,
            NombreContacto = request.NombreContacto,
            Telefono = request.Telefono,
            Correo = request.Correo,
            Direccion = request.Direccion
        };

        _db.Proveedores.Add(proveedor);

        await _auditoria.Registrar("PROVEEDOR_CREADO", null, null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"RUC {request.Ruc} - {request.RazonSocial}");

        await _db.SaveChangesAsync();

        return Ok(new ProveedorItem
        {
            Id = proveedor.Id,
            Ruc = proveedor.Ruc,
            RazonSocial = proveedor.RazonSocial,
            NombreContacto = proveedor.NombreContacto,
            Telefono = proveedor.Telefono,
            Correo = proveedor.Correo,
            Direccion = proveedor.Direccion,
            Activo = proveedor.Activo,
            FechaCreacion = proveedor.FechaCreacion
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(int id, EditarProveedorRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "INVENTARIO_GESTIONAR"))
            return Forbid();

        var proveedor = await _db.Proveedores.FindAsync(id);
        if (proveedor == null) return NotFound();

        proveedor.RazonSocial = request.RazonSocial;
        proveedor.NombreContacto = request.NombreContacto;
        proveedor.Telefono = request.Telefono;
        proveedor.Correo = request.Correo;
        proveedor.Direccion = request.Direccion;

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Proveedor actualizado." });
    }

    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] bool activo)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "INVENTARIO_GESTIONAR"))
            return Forbid();

        var proveedor = await _db.Proveedores.FindAsync(id);
        if (proveedor == null) return NotFound();

        proveedor.Activo = activo;
        await _db.SaveChangesAsync();
        return Ok(new { mensaje = activo ? "Proveedor activado." : "Proveedor desactivado." });
    }
}