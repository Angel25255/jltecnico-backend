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
[Route("api/cotizaciones")]
[Authorize]
public class CotizacionesController : ControllerBase
{
    private const decimal TASA_IGV = 0.18m;
    private const int DIAS_VALIDEZ_DEFECTO = 15;

    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;
    private readonly AuditoriaService _auditoria;
    private readonly CotizacionPdfService _pdfService;

    public CotizacionesController(AppDbContext db, PermisosService permisosService,
        AuditoriaService auditoria, CotizacionPdfService pdfService)
    {
        _db = db;
        _permisosService = permisosService;
        _auditoria = auditoria;
        _pdfService = pdfService;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
    private int UsuarioActualId() => int.Parse(User.FindFirst("userId")!.Value);

    // -----------------------------------------------------------
    // Crear cotización (NO descuenta stock, es solo una propuesta)
    // -----------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<CotizacionItem>> Crear(CrearCotizacionRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "COTIZACIONES_CREAR"))
            return Forbid();

        if (request.Items == null || request.Items.Count == 0)
            return BadRequest(new { mensaje = "Agrega al menos un producto a la cotización." });

        var cliente = await _db.Clientes.FindAsync(request.ClienteId);
        if (cliente == null || !cliente.Activo)
            return BadRequest(new { mensaje = "El cliente seleccionado no es válido o está desactivado." });

        var idsProductos = request.Items.Select(i => i.ProductoId).ToList();
        var productos = await _db.Productos.Where(p => idsProductos.Contains(p.Id)).ToListAsync();

        foreach (var item in request.Items)
        {
            var producto = productos.FirstOrDefault(p => p.Id == item.ProductoId);
            if (producto == null)
                return BadRequest(new { mensaje = "Un producto de la cotización ya no existe." });
            if (item.Cantidad <= 0)
                return BadRequest(new { mensaje = $"La cantidad de '{producto.Nombre}' debe ser mayor a 0." });
        }

        var cotizacion = new Cotizacion
        {
            ClienteId = request.ClienteId,
            VendedorUsuarioId = UsuarioActualId(),
            FechaCotizacion = DateTime.UtcNow,
            FechaValidez = DateTime.UtcNow.AddDays(DIAS_VALIDEZ_DEFECTO),
            Estado = "Pendiente"
        };

        decimal subTotal = 0, igvTotal = 0, total = 0;

        foreach (var item in request.Items)
        {
            var producto = productos.First(p => p.Id == item.ProductoId);
            decimal totalLinea = producto.PrecioUnitario * item.Cantidad;
            decimal subtotalLinea = Math.Round(totalLinea / (1 + TASA_IGV), 2);
            decimal igvLinea = totalLinea - subtotalLinea;

            cotizacion.Detalles.Add(new CotizacionDetalle
            {
                ProductoId = producto.Id,
                NombreProducto = producto.Nombre,
                Cantidad = item.Cantidad,
                PrecioUnitario = producto.PrecioUnitario,
                Subtotal = totalLinea
            });

            subTotal += subtotalLinea;
            igvTotal += igvLinea;
            total += totalLinea;
        }

        cotizacion.SubTotal = Math.Round(subTotal, 2);
        cotizacion.Igv = Math.Round(igvTotal, 2);
        cotizacion.Total = Math.Round(total, 2);

        _db.Cotizaciones.Add(cotizacion);

        await _auditoria.Registrar("COTIZACION_CREADA", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Cliente #{cliente.Id} - Total S/ {cotizacion.Total:F2}");

        await _db.SaveChangesAsync();

        var vendedor = await _db.Usuarios.FindAsync(UsuarioActualId());

        return Ok(MapearItem(cotizacion, cliente.NombreORazonSocial, vendedor?.NombreCompleto ?? ""));
    }

    // -----------------------------------------------------------
    // Listado
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<List<CotizacionItem>>> Listar([FromQuery] int limite = 50)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "COTIZACIONES_VER"))
            return Forbid();

        var cotizaciones = await _db.Cotizaciones
            .Include(c => c.Cliente)
            .Include(c => c.VendedorUsuario)
            .Include(c => c.Detalles)
            .OrderByDescending(c => c.FechaCotizacion)
            .Take(limite)
            .ToListAsync();

        var resultado = cotizaciones.Select(c =>
            MapearItem(c, c.Cliente?.NombreORazonSocial ?? "—", c.VendedorUsuario?.NombreCompleto ?? "—")
        ).ToList();

        return Ok(resultado);
    }

    // -----------------------------------------------------------
    // Cambiar estado: Pendiente -> Aprobada, o cualquiera -> Anulada
    // -----------------------------------------------------------
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoCotizacionRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "COTIZACIONES_CREAR"))
            return Forbid();

        var cotizacion = await _db.Cotizaciones.FindAsync(id);
        if (cotizacion == null) return NotFound();

        if (cotizacion.Estado == "Facturada")
            return BadRequest(new { mensaje = "Esta cotización ya fue facturada, no se puede cambiar su estado." });

        if (request.Estado != "Aprobada" && request.Estado != "Anulada" && request.Estado != "Pendiente")
            return BadRequest(new { mensaje = "Estado inválido." });

        cotizacion.Estado = request.Estado;

        await _auditoria.Registrar("COTIZACION_ESTADO_CAMBIADO", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Cotizacion #{id} -> {request.Estado}");

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = $"Cotización marcada como {request.Estado}." });
    }

    // -----------------------------------------------------------
    // Convertir en venta real: solo si está Aprobada.
    // Aquí SÍ se valida y descuenta stock (como en Ventas).
    // -----------------------------------------------------------
    [HttpPost("{id}/convertir-a-venta")]
    public async Task<ActionResult<VentaItem>> ConvertirAVenta(int id)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_CREAR"))
            return Forbid();

        var cotizacion = await _db.Cotizaciones
            .Include(c => c.Detalles)
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cotizacion == null) return NotFound();

        if (cotizacion.Estado != "Aprobada")
            return BadRequest(new { mensaje = "Solo se pueden convertir en venta las cotizaciones Aprobadas." });

        // Validar stock de todos los productos antes de descontar nada
        var idsProductos = cotizacion.Detalles.Select(d => d.ProductoId).ToList();
        var productos = await _db.Productos.Where(p => idsProductos.Contains(p.Id)).ToListAsync();

        foreach (var detalle in cotizacion.Detalles)
        {
            var producto = productos.FirstOrDefault(p => p.Id == detalle.ProductoId);
            if (producto == null || !producto.Activo)
                return BadRequest(new { mensaje = $"El producto '{detalle.NombreProducto}' ya no está disponible." });

            if (detalle.Cantidad > producto.Stock)
                return BadRequest(new
                {
                    mensaje = $"Stock insuficiente de '{producto.Nombre}'. Disponible: {producto.Stock}, requerido: {detalle.Cantidad}."
                });
        }

        var venta = new Venta
        {
            ClienteId = cotizacion.ClienteId,
            VendedorUsuarioId = UsuarioActualId(),
            FechaVenta = DateTime.UtcNow,
            SubTotal = cotizacion.SubTotal,
            Igv = cotizacion.Igv,
            Total = cotizacion.Total,
            Estado = "Completada"
        };

        foreach (var detalle in cotizacion.Detalles)
        {
            var producto = productos.First(p => p.Id == detalle.ProductoId);
            producto.Stock -= detalle.Cantidad;

            venta.Detalles.Add(new VentaDetalle
            {
                ProductoId = detalle.ProductoId,
                NombreProducto = detalle.NombreProducto,
                Cantidad = detalle.Cantidad,
                PrecioUnitario = detalle.PrecioUnitario,
                CostoUnitario = producto.CostoUnitario, // costo vigente al momento de facturar
                Subtotal = detalle.Subtotal
            });
        }

        _db.Ventas.Add(venta);
        await _db.SaveChangesAsync(); // para obtener venta.Id

        cotizacion.Estado = "Facturada";
        cotizacion.VentaId = venta.Id;

        await _auditoria.Registrar("COTIZACION_CONVERTIDA_VENTA", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Cotizacion #{id} -> Venta #{venta.Id}");

        await _db.SaveChangesAsync();

        var vendedor = await _db.Usuarios.FindAsync(UsuarioActualId());

        return Ok(new VentaItem
        {
            Id = venta.Id,
            NombreCliente = cotizacion.Cliente?.NombreORazonSocial ?? "",
            NombreVendedor = vendedor?.NombreCompleto ?? "",
            FechaVenta = venta.FechaVenta,
            SubTotal = venta.SubTotal,
            Igv = venta.Igv,
            Total = venta.Total,
            Estado = venta.Estado,
            Detalles = venta.Detalles.Select(d => new VentaDetalleItem
            {
                Id = d.Id,
                NombreProducto = d.NombreProducto,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                CostoUnitario = d.CostoUnitario,
                Subtotal = d.Subtotal,
                AgregadoEnCampo = d.AgregadoEnCampo
            }).ToList()
        });
    }

    // -----------------------------------------------------------
    // Descargar el PDF de la cotización (para entregar al cliente)
    // -----------------------------------------------------------
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DescargarPdf(int id)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "COTIZACIONES_VER"))
            return Forbid();

        var cotizacion = await _db.Cotizaciones
            .Include(c => c.Cliente)
            .Include(c => c.VendedorUsuario)
            .Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cotizacion == null) return NotFound();

        byte[] pdfBytes = _pdfService.GenerarCotizacionPdf(cotizacion);
        return File(pdfBytes, "application/pdf", $"Cotizacion-{cotizacion.Id:D6}.pdf");
    }

    private CotizacionItem MapearItem(Cotizacion c, string nombreCliente, string nombreVendedor)
    {
        return new CotizacionItem
        {
            Id = c.Id,
            NombreCliente = nombreCliente,
            NombreVendedor = nombreVendedor,
            FechaCotizacion = c.FechaCotizacion,
            FechaValidez = c.FechaValidez,
            SubTotal = c.SubTotal,
            Igv = c.Igv,
            Total = c.Total,
            Estado = c.Estado,
            VentaId = c.VentaId,
            Detalles = c.Detalles.Select(d => new CotizacionDetalleItem
            {
                NombreProducto = d.NombreProducto,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Subtotal = d.Subtotal
            }).ToList()
        };
    }
}