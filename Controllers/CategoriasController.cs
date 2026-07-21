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
[Route("api/categorias")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;

    public CategoriasController(AppDbContext db, PermisosService permisosService)
    {
        _db = db;
        _permisosService = permisosService;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    [HttpGet]
    public async Task<ActionResult<List<CategoriaItem>>> Listar()
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_VER"))
            return Forbid();

        var categorias = await _db.Categorias
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaItem { Id = c.Id, Nombre = c.Nombre, Descripcion = c.Descripcion, Activo = c.Activo })
            .ToListAsync();

        return Ok(categorias);
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaItem>> Crear(CrearEditarCategoriaRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_CREAR"))
            return Forbid();

        bool existe = await _db.Categorias.AnyAsync(c => c.Nombre == request.Nombre);
        if (existe) return BadRequest(new { mensaje = "Ya existe una categoría con ese nombre." });

        var categoria = new Categoria { Nombre = request.Nombre, Descripcion = request.Descripcion };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();

        return Ok(new CategoriaItem { Id = categoria.Id, Nombre = categoria.Nombre, Descripcion = categoria.Descripcion, Activo = categoria.Activo });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(int id, CrearEditarCategoriaRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_CREAR"))
            return Forbid();

        var categoria = await _db.Categorias.FindAsync(id);
        if (categoria == null) return NotFound();

        categoria.Nombre = request.Nombre;
        categoria.Descripcion = request.Descripcion;
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Categoría actualizada." });
    }

    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] bool activo)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_CREAR"))
            return Forbid();

        var categoria = await _db.Categorias.FindAsync(id);
        if (categoria == null) return NotFound();

        categoria.Activo = activo;
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = activo ? "Categoría activada." : "Categoría desactivada." });
    }
}