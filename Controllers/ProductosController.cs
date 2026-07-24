using System.Security.Claims;
using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using JLTecnico.Auth.Models;
using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

// Catálogo de productos para poder vender. Este mismo modelo se
// ampliará en el módulo completo de Inventario (Sprint 3) sin
// perder los datos ya cargados aquí (proveedores, kardex, etc.
// se agregan sobre esta misma tabla más adelante).
[ApiController]
[Route("api/productos")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;

    public ProductosController(AppDbContext db, PermisosService permisosService)
    {
        _db = db;
        _permisosService = permisosService;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    // -----------------------------------------------------------
    // Listado con búsqueda opcional por nombre o código
    // GET /api/productos?busqueda=cable
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<List<ProductoItem>>> Listar([FromQuery] string? busqueda)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "PRODUCTOS_VER"))
            return Forbid();

        var query = _db.Productos.Include(p => p.Categoria).AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            query = query.Where(p =>
                p.Nombre.Contains(busqueda) ||
                (p.Codigo != null && p.Codigo.Contains(busqueda)));
        }

        var productos = await query
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoItem
            {
                Id = p.Id,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                CategoriaId = p.CategoriaId,
                NombreCategoria = p.Categoria != null ? p.Categoria.Nombre : null,
                UnidadMedida = p.UnidadMedida,
                PrecioUnitario = p.PrecioUnitario,
                CostoUnitario = p.CostoUnitario,
                Stock = p.Stock,
                Activo = p.Activo
            })
            .ToListAsync();

        return Ok(productos);
    }

    [HttpPost]
    public async Task<ActionResult<ProductoItem>> Crear(CrearEditarProductoRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "PRODUCTOS_GESTIONAR"))
            return Forbid();

        var producto = new Producto
        {
            Codigo = request.Codigo,
            Nombre = request.Nombre,
            CategoriaId = request.CategoriaId,
            UnidadMedida = request.UnidadMedida,
            PrecioUnitario = request.PrecioUnitario,
            CostoUnitario = request.CostoUnitario,
            Stock = request.Stock,
            Activo = true
        };

        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();

        var categoria = request.CategoriaId.HasValue ? await _db.Categorias.FindAsync(request.CategoriaId.Value) : null;

        return Ok(new ProductoItem
        {
            Id = producto.Id,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            CategoriaId = producto.CategoriaId,
            NombreCategoria = categoria?.Nombre,
            UnidadMedida = producto.UnidadMedida,
            PrecioUnitario = producto.PrecioUnitario,
            CostoUnitario = producto.CostoUnitario,
            Stock = producto.Stock,
            Activo = producto.Activo
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(int id, CrearEditarProductoRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "PRODUCTOS_GESTIONAR"))
            return Forbid();

        var producto = await _db.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        producto.Codigo = request.Codigo;
        producto.Nombre = request.Nombre;
        producto.CategoriaId = request.CategoriaId;
        producto.UnidadMedida = request.UnidadMedida;
        producto.PrecioUnitario = request.PrecioUnitario;
        producto.CostoUnitario = request.CostoUnitario;
        producto.Stock = request.Stock;

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Producto actualizado." });
    }

    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] bool activo)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "PRODUCTOS_GESTIONAR"))
            return Forbid();

        var producto = await _db.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        producto.Activo = activo;
        await _db.SaveChangesAsync();
        return Ok(new { mensaje = activo ? "Producto activado." : "Producto desactivado." });
    }
}