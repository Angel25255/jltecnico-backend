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
[Route("api/compras")]
[Authorize]
public class ComprasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;
    private readonly AuditoriaService _auditoria;

    public ComprasController(AppDbContext db, PermisosService permisosService, AuditoriaService auditoria)
    {
        _db = db;
        _permisosService = permisosService;
        _auditoria = auditoria;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
    private int UsuarioActualId() => int.Parse(User.FindFirst("userId")!.Value);

    // -----------------------------------------------------------
    // Registrar una compra: SUBE el stock de cada producto y
    // actualiza su costo unitario vigente (para calcular ganancia
    // real en ventas futuras).
    // -----------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<CompraItem>> Crear(CrearCompraRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "COMPRAS_GESTIONAR"))
            return Forbid();

        if (request.Items == null || request.Items.Count == 0)
            return BadRequest(new { mensaje = "Agrega al menos un producto a la compra." });

        var proveedor = await _db.Proveedores.FindAsync(request.ProveedorId);
        if (proveedor == null || !proveedor.Activo)
            return BadRequest(new { mensaje = "El proveedor seleccionado no es válido o está desactivado." });

        var idsProductos = request.Items.Select(i => i.ProductoId).ToList();
        var productos = await _db.Productos.Where(p => idsProductos.Contains(p.Id)).ToListAsync();

        foreach (var item in request.Items)
        {
            var producto = productos.FirstOrDefault(p => p.Id == item.ProductoId);
            if (producto == null)
                return BadRequest(new { mensaje = "Un producto de la compra ya no existe." });
            if (item.Cantidad <= 0)
                return BadRequest(new { mensaje = $"La cantidad de '{producto.Nombre}' debe ser mayor a 0." });
            if (item.CostoUnitario < 0)
                return BadRequest(new { mensaje = $"El costo de '{producto.Nombre}' no puede ser negativo." });
        }

        var compra = new Compra
        {
            ProveedorId = request.ProveedorId,
            UsuarioId = UsuarioActualId(),
            FechaCompra = DateTime.UtcNow,
            NumeroDocumento = request.NumeroDocumento
        };

        decimal total = 0;

        foreach (var item in request.Items)
        {
            var producto = productos.First(p => p.Id == item.ProductoId);
            decimal subtotalLinea = item.CostoUnitario * item.Cantidad;

            compra.Detalles.Add(new CompraDetalle
            {
                ProductoId = producto.Id,
                NombreProducto = producto.Nombre,
                Cantidad = item.Cantidad,
                CostoUnitario = item.CostoUnitario,
                Subtotal = subtotalLinea
            });

            // Sube el stock y actualiza el costo vigente del producto
            producto.Stock += item.Cantidad;
            producto.CostoUnitario = item.CostoUnitario;

            total += subtotalLinea;
        }

        compra.Total = Math.Round(total, 2);
        _db.Compras.Add(compra);

        await _auditoria.Registrar("COMPRA_REGISTRADA", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Proveedor #{proveedor.Id} - Total S/ {compra.Total:F2}");

        await _db.SaveChangesAsync();

        var usuario = await _db.Usuarios.FindAsync(UsuarioActualId());

        return Ok(MapearItem(compra, proveedor.RazonSocial, usuario?.NombreCompleto ?? ""));
    }

    // -----------------------------------------------------------
    // Listado de compras (más recientes primero)
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<List<CompraItem>>> Listar([FromQuery] int limite = 50)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "COMPRAS_VER"))
            return Forbid();

        var compras = await _db.Compras
            .Include(c => c.Proveedor)
            .Include(c => c.Usuario)
            .Include(c => c.Detalles)
            .OrderByDescending(c => c.FechaCompra)
            .Take(limite)
            .ToListAsync();

        var resultado = compras.Select(c =>
            MapearItem(c, c.Proveedor?.RazonSocial ?? "—", c.Usuario?.NombreCompleto ?? "—")
        ).ToList();

        return Ok(resultado);
    }

    private CompraItem MapearItem(Compra c, string nombreProveedor, string nombreUsuario)
    {
        return new CompraItem
        {
            Id = c.Id,
            NombreProveedor = nombreProveedor,
            NombreUsuario = nombreUsuario,
            FechaCompra = c.FechaCompra,
            Total = c.Total,
            NumeroDocumento = c.NumeroDocumento,
            Detalles = c.Detalles.Select(d => new CompraDetalleItem
            {
                NombreProducto = d.NombreProducto,
                Cantidad = d.Cantidad,
                CostoUnitario = d.CostoUnitario,
                Subtotal = d.Subtotal
            }).ToList()
        };
    }
}