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
[Route("api/ventas")]
[Authorize]
public class VentasController : ControllerBase
{
    private const decimal TASA_IGV = 0.18m;

    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;
    private readonly AuditoriaService _auditoria;
    private readonly BoletaPdfService _boletaPdfService;

    public VentasController(AppDbContext db, PermisosService permisosService, AuditoriaService auditoria, BoletaPdfService boletaPdfService)
    {
        _db = db;
        _permisosService = permisosService;
        _auditoria = auditoria;
        _boletaPdfService = boletaPdfService;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
    private int UsuarioActualId() => int.Parse(User.FindFirst("userId")!.Value);

    // -----------------------------------------------------------
    // Registrar una venta: valida stock, descuenta stock,
    // calcula subtotal/IGV/total (el precio del producto YA
    // incluye IGV, según la regla de negocio de JL Técnico).
    // -----------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<VentaItem>> Crear(CrearVentaRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_CREAR"))
            return Forbid();

        if (request.Items == null || request.Items.Count == 0)
            return BadRequest(new { mensaje = "Agrega al menos un producto a la venta." });

        var cliente = await _db.Clientes.FindAsync(request.ClienteId);
        if (cliente == null || !cliente.Activo)
            return BadRequest(new { mensaje = "El cliente seleccionado no es válido o está desactivado." });

        // Cargar todos los productos involucrados de una vez
        var idsProductos = request.Items.Select(i => i.ProductoId).ToList();
        var productos = await _db.Productos.Where(p => idsProductos.Contains(p.Id)).ToListAsync();

        // Validar stock ANTES de descontar nada (para no dejar la venta a medias)
        foreach (var item in request.Items)
        {
            var producto = productos.FirstOrDefault(p => p.Id == item.ProductoId);
            if (producto == null)
                return BadRequest(new { mensaje = $"Un producto de la venta ya no existe." });

            if (!producto.Activo)
                return BadRequest(new { mensaje = $"El producto '{producto.Nombre}' está desactivado." });

            if (item.Cantidad <= 0)
                return BadRequest(new { mensaje = $"La cantidad de '{producto.Nombre}' debe ser mayor a 0." });

            if (item.Cantidad > producto.Stock)
                return BadRequest(new
                {
                    mensaje = $"Stock insuficiente de '{producto.Nombre}'. Disponible: {producto.Stock}, solicitado: {item.Cantidad}."
                });
        }

        // Ya validado todo: recién ahora se arma la venta y se descuenta stock
        var venta = new Venta
        {
            ClienteId = request.ClienteId,
            VendedorUsuarioId = UsuarioActualId(),
            FechaVenta = DateTime.UtcNow,
            Estado = "Completada"
        };

        decimal subTotalVenta = 0;
        decimal igvVenta = 0;
        decimal totalVenta = 0;

        foreach (var item in request.Items)
        {
            var producto = productos.First(p => p.Id == item.ProductoId);

            decimal totalLinea = producto.PrecioUnitario * item.Cantidad; // incluye IGV
            decimal subTotalLinea = Math.Round(totalLinea / (1 + TASA_IGV), 2);
            decimal igvLinea = totalLinea - subTotalLinea;

            venta.Detalles.Add(new VentaDetalle
            {
                ProductoId = producto.Id,
                NombreProducto = producto.Nombre,
                Cantidad = item.Cantidad,
                PrecioUnitario = producto.PrecioUnitario,
                CostoUnitario = producto.CostoUnitario, // se "congela" el costo al momento de vender
                Subtotal = totalLinea
            });

            producto.Stock -= item.Cantidad;

            subTotalVenta += subTotalLinea;
            igvVenta += igvLinea;
            totalVenta += totalLinea;
        }

        venta.SubTotal = Math.Round(subTotalVenta, 2);
        venta.Igv = Math.Round(igvVenta, 2);
        venta.Total = Math.Round(totalVenta, 2);

        _db.Ventas.Add(venta);

        await _auditoria.Registrar("VENTA_REGISTRADA", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Cliente #{cliente.Id} - Total S/ {venta.Total:F2}");

        await _db.SaveChangesAsync(); // necesitamos venta.Id ya generado

        int? ordenServicioId = null;

        if (request.RequiereOrdenServicio)
        {
            var orden = new Models.OrdenServicio
            {
                ClienteId = cliente.Id,
                VentaId = venta.Id,
                CreadoPorUsuarioId = UsuarioActualId(),
                Descripcion = string.IsNullOrWhiteSpace(request.DescripcionServicio)
                    ? "Instalación de los productos comprados"
                    : request.DescripcionServicio,
                DireccionInstalacion = request.DireccionInstalacion,
                Estado = "Pendiente",
                TokenSeguimiento = Guid.NewGuid().ToString("N")
            };

            _db.OrdenesServicio.Add(orden);
            await _db.SaveChangesAsync();
            ordenServicioId = orden.Id;

            await _auditoria.Registrar("ORDEN_SERVICIO_CREADA_DESDE_VENTA", UsuarioActualId(), null,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
                $"Orden #{orden.Id} desde Venta #{venta.Id}");
        }

        var vendedor = await _db.Usuarios.FindAsync(UsuarioActualId());

        return Ok(new VentaItem
        {
            Id = venta.Id,
            NombreCliente = cliente.NombreORazonSocial,
            NombreVendedor = vendedor?.NombreCompleto ?? "",
            FechaVenta = venta.FechaVenta,
            SubTotal = venta.SubTotal,
            Igv = venta.Igv,
            Total = venta.Total,
            Estado = venta.Estado,
            OrdenServicioId = ordenServicioId,
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
    // Listado de ventas (más recientes primero)
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<List<VentaItem>>> Listar([FromQuery] int limite = 50)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_VER"))
            return Forbid();

        var ventas = await _db.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.VendedorUsuario)
            .Include(v => v.Detalles)
            .OrderByDescending(v => v.FechaVenta)
            .Take(limite)
            .ToListAsync();

        var resultado = ventas.Select(v => new VentaItem
        {
            Id = v.Id,
            NombreCliente = v.Cliente?.NombreORazonSocial ?? "—",
            NombreVendedor = v.VendedorUsuario?.NombreCompleto ?? "—",
            FechaVenta = v.FechaVenta,
            SubTotal = v.SubTotal,
            Igv = v.Igv,
            Total = v.Total,
            Estado = v.Estado,
            Detalles = v.Detalles.Select(d => new VentaDetalleItem
            {
                Id = d.Id,
                NombreProducto = d.NombreProducto,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                CostoUnitario = d.CostoUnitario,
                Subtotal = d.Subtotal,
                AgregadoEnCampo = d.AgregadoEnCampo
            }).ToList()
        }).ToList();

        return Ok(resultado);
    }

    // -----------------------------------------------------------
    // Descargar la boleta de una venta en PDF
    // GET /api/ventas/{id}/pdf
    // -----------------------------------------------------------
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DescargarPdf(int id)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_VER"))
            return Forbid();

        var venta = await _db.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.VendedorUsuario)
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (venta == null) return NotFound();

        byte[] pdfBytes = _boletaPdfService.GenerarBoletaPdf(venta);

        return File(pdfBytes, "application/pdf", $"Boleta-{venta.Id:D6}.pdf");
    }

    // -----------------------------------------------------------
    // Anular venta: devuelve el stock de cada producto vendido
    // -----------------------------------------------------------
    [HttpPatch("{id}/anular")]
    public async Task<IActionResult> Anular(int id)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "VENTAS_CREAR"))
            return Forbid();

        var venta = await _db.Ventas.Include(v => v.Detalles).FirstOrDefaultAsync(v => v.Id == id);
        if (venta == null) return NotFound();

        if (venta.Estado == "Anulada")
            return BadRequest(new { mensaje = "Esta venta ya estaba anulada." });

        // Devolver el stock de cada producto de la venta
        foreach (var detalle in venta.Detalles)
        {
            var producto = await _db.Productos.FindAsync(detalle.ProductoId);
            if (producto != null)
            {
                producto.Stock += detalle.Cantidad;
            }
        }

        venta.Estado = "Anulada";

        await _auditoria.Registrar("VENTA_ANULADA", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Venta #{venta.Id} - Total S/ {venta.Total:F2}");

        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Venta anulada y stock devuelto." });
    }
}