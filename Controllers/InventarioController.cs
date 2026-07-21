using System.Security.Claims;
using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLTecnico.Auth.Services;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/inventario")]
[Authorize]
public class InventarioController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;

    public InventarioController(AppDbContext db, PermisosService permisosService)
    {
        _db = db;
        _permisosService = permisosService;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    // -----------------------------------------------------------
    // Resumen por producto: totales acumulados de SIEMPRE (no por
    // rango de fechas) — cuánto se compró, cuánto se vendió, y la
    // ganancia de cada producto, en su propia fila.
    // -----------------------------------------------------------
    [HttpGet("resumen-productos")]
    public async Task<ActionResult<List<ResumenProductoInventarioItem>>> ResumenPorProducto()
    {
        if (!await _permisosService.TienePermiso(RolActual(), "INVENTARIO_VER"))
            return Forbid();

        var productos = await _db.Productos
            .Include(p => p.Categoria)
            .Where(p => p.Activo)
            .ToListAsync();

        var comprasDetalle = await _db.CompraDetalles.ToListAsync();
        var ventasDetalle = await _db.VentaDetalles
            .Include(d => d.Venta)
            .Where(d => d.Venta!.Estado == "Completada")
            .ToListAsync();

        var resultado = productos.Select(p =>
        {
            var comprasProducto = comprasDetalle.Where(cd => cd.ProductoId == p.Id).ToList();
            var ventasProducto = ventasDetalle.Where(vd => vd.ProductoId == p.Id).ToList();

            decimal totalVendido = ventasProducto.Sum(v => v.Subtotal);
            decimal costoDeLoVendido = ventasProducto.Sum(v => v.CostoUnitario * v.Cantidad);

            return new ResumenProductoInventarioItem
            {
                ProductoId = p.Id,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                NombreCategoria = p.Categoria?.Nombre,
                StockActual = p.Stock,
                CostoUnitarioActual = p.CostoUnitario,
                PrecioVentaActual = p.PrecioUnitario,
                CantidadComprada = comprasProducto.Sum(c => c.Cantidad),
                TotalComprado = comprasProducto.Sum(c => c.Subtotal),
                CantidadVendida = ventasProducto.Sum(v => v.Cantidad),
                TotalVendido = totalVendido,
                GananciaTotal = totalVendido - costoDeLoVendido
            };
        })
        .OrderByDescending(r => r.GananciaTotal)
        .ToList();

        return Ok(resultado);
    }

    // -----------------------------------------------------------
    // Kardex detallado de UN producto: todos sus movimientos
    // (compras = entrada, ventas = salida) en orden cronológico.
    // -----------------------------------------------------------
    [HttpGet("kardex/{productoId}")]
    public async Task<ActionResult<List<MovimientoKardexItem>>> KardexDeProducto(int productoId)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "INVENTARIO_VER"))
            return Forbid();

        var entradas = await _db.CompraDetalles
            .Include(cd => cd.Compra)
            .ThenInclude(c => c!.Proveedor)
            .Where(cd => cd.ProductoId == productoId)
            .Select(cd => new MovimientoKardexItem
            {
                Fecha = cd.Compra!.FechaCompra,
                Tipo = "Entrada (Compra)",
                Cantidad = cd.Cantidad,
                Referencia = $"Compra #{cd.CompraId}",
                NombreTercero = cd.Compra.Proveedor != null ? cd.Compra.Proveedor.RazonSocial : null
            })
            .ToListAsync();

        var salidas = await _db.VentaDetalles
            .Include(vd => vd.Venta)
            .ThenInclude(v => v!.Cliente)
            .Where(vd => vd.ProductoId == productoId && vd.Venta!.Estado == "Completada")
            .Select(vd => new MovimientoKardexItem
            {
                Fecha = vd.Venta!.FechaVenta,
                Tipo = "Salida (Venta)",
                Cantidad = -vd.Cantidad,
                Referencia = $"Venta #{vd.VentaId}",
                NombreTercero = vd.Venta.Cliente != null ? vd.Venta.Cliente.NombreORazonSocial : null
            })
            .ToListAsync();

        var movimientos = entradas.Concat(salidas)
            .OrderByDescending(m => m.Fecha)
            .ToList();

        return Ok(movimientos);
    }
}