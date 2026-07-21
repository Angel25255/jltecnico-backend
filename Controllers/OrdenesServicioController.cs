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
[Route("api/ordenes-servicio")]
[Authorize]
public class OrdenesServicioController : ControllerBase
{
    private const decimal TASA_IGV = 0.18m;
    private static readonly string[] ESTADOS_VALIDOS =
        { "Pendiente", "Asignada", "EnCamino", "EnProceso", "Completada", "Cancelada" };

    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;
    private readonly AuditoriaService _auditoria;

    public OrdenesServicioController(AppDbContext db, PermisosService permisosService, AuditoriaService auditoria)
    {
        _db = db;
        _permisosService = permisosService;
        _auditoria = auditoria;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
    private int UsuarioActualId() => int.Parse(User.FindFirst("userId")!.Value);

    // -----------------------------------------------------------
    // Ventas del mostrador que todavía NO tienen una orden de
    // servicio asociada (para poder crear la orden manualmente
    // si no se marcó "requiere instalación" al momento de vender).
    // -----------------------------------------------------------
    [HttpGet("ventas-disponibles")]
    public async Task<ActionResult<List<VentaSinOrdenItem>>> VentasDisponibles()
    {
        if (!await _permisosService.TienePermiso(RolActual(), "OS_GESTIONAR"))
            return Forbid();

        var idsVentasConOrden = await _db.OrdenesServicio.Select(o => o.VentaId).ToListAsync();

        var ventas = await _db.Ventas
            .Include(v => v.Cliente)
            .Where(v => v.Estado == "Completada" && !idsVentasConOrden.Contains(v.Id))
            .OrderByDescending(v => v.FechaVenta)
            .Take(50)
            .Select(v => new VentaSinOrdenItem
            {
                VentaId = v.Id,
                NombreCliente = v.Cliente!.NombreORazonSocial,
                Total = v.Total,
                FechaVenta = v.FechaVenta
            })
            .ToListAsync();

        return Ok(ventas);
    }

    // -----------------------------------------------------------
    // Crear la orden de servicio a partir de una venta existente
    // -----------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<OrdenServicioItem>> Crear(CrearOrdenServicioRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "OS_GESTIONAR"))
            return Forbid();

        var venta = await _db.Ventas.FindAsync(request.VentaId);
        if (venta == null || venta.Estado != "Completada")
            return BadRequest(new { mensaje = "La venta indicada no existe o no está completada." });

        bool yaTieneOrden = await _db.OrdenesServicio.AnyAsync(o => o.VentaId == request.VentaId);
        if (yaTieneOrden)
            return BadRequest(new { mensaje = "Esta venta ya tiene una orden de servicio asociada." });

        if (request.TecnicoUsuarioId.HasValue)
        {
            var tecnico = await _db.Usuarios.FindAsync(request.TecnicoUsuarioId.Value);
            if (tecnico == null || tecnico.Rol != "Tecnico" || !tecnico.Activo)
                return BadRequest(new { mensaje = "El técnico seleccionado no es válido." });
        }

        var orden = new OrdenServicio
        {
            ClienteId = venta.ClienteId,
            VentaId = venta.Id,
            TecnicoUsuarioId = request.TecnicoUsuarioId,
            CreadoPorUsuarioId = UsuarioActualId(),
            Descripcion = request.Descripcion,
            DireccionInstalacion = request.DireccionInstalacion,
            FechaProgramada = request.FechaProgramada,
            Estado = request.TecnicoUsuarioId.HasValue ? "Asignada" : "Pendiente",
            TokenSeguimiento = Guid.NewGuid().ToString("N"),
            DestinoLat = request.DestinoLat,
            DestinoLng = request.DestinoLng
        };

        _db.OrdenesServicio.Add(orden);

        await _auditoria.Registrar("ORDEN_SERVICIO_CREADA", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Orden ligada a Venta #{venta.Id}");

        await _db.SaveChangesAsync();

        return Ok(await ObtenerItemCompleto(orden.Id));
    }

    // -----------------------------------------------------------
    // Listado. Un Técnico solo ve SUS propias órdenes asignadas.
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<List<OrdenServicioItem>>> Listar([FromQuery] string? estado)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "OS_VER"))
            return Forbid();

        var query = _db.OrdenesServicio
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoUsuario)
            .Include(o => o.Venta).ThenInclude(v => v!.Detalles)
            .AsQueryable();

        if (RolActual() == "Tecnico")
            query = query.Where(o => o.TecnicoUsuarioId == UsuarioActualId());

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(o => o.Estado == estado);

        var ordenes = await query.OrderByDescending(o => o.FechaCreacion).ToListAsync();

        var creadores = await _db.Usuarios
            .Where(u => ordenes.Select(o => o.CreadoPorUsuarioId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto);

        return Ok(ordenes.Select(o => MapearItem(o, creadores)).ToList());
    }

    // -----------------------------------------------------------
    // Obtener una orden individual (usado para refrescar el mapa
    // en vivo dentro del sistema, sin pasar por el link público)
    // -----------------------------------------------------------
    [HttpGet("{id}")]
    public async Task<ActionResult<OrdenServicioItem>> ObtenerPorId(int id)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "OS_VER"))
            return Forbid();

        var orden = await _db.OrdenesServicio.FindAsync(id);
        if (orden == null) return NotFound();

        if (RolActual() == "Tecnico" && orden.TecnicoUsuarioId != UsuarioActualId())
            return Forbid();

        return Ok(await ObtenerItemCompleto(id));
    }

    // -----------------------------------------------------------
    // Asignar (o reasignar) técnico
    // -----------------------------------------------------------
    [HttpPatch("{id}/asignar-tecnico")]
    public async Task<IActionResult> AsignarTecnico(int id, AsignarTecnicoRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "OS_GESTIONAR"))
            return Forbid();

        var orden = await _db.OrdenesServicio.FindAsync(id);
        if (orden == null) return NotFound();

        if (orden.Estado == "Completada" || orden.Estado == "Cancelada")
            return BadRequest(new { mensaje = "No se puede reasignar una orden cerrada." });

        var tecnico = await _db.Usuarios.FindAsync(request.TecnicoUsuarioId);
        if (tecnico == null || tecnico.Rol != "Tecnico" || !tecnico.Activo)
            return BadRequest(new { mensaje = "El técnico seleccionado no es válido." });

        orden.TecnicoUsuarioId = request.TecnicoUsuarioId;
        if (orden.Estado == "Pendiente") orden.Estado = "Asignada";

        await _db.SaveChangesAsync();
        return Ok(await ObtenerItemCompleto(id));
    }

    // -----------------------------------------------------------
    // Cambiar estado. Ya NO genera ninguna venta (la venta ya
    // existe desde que se creó la orden) - solo actualiza estado
    // y, al completar, suma 1 al contador del técnico.
    // -----------------------------------------------------------
    [HttpPatch("{id}/estado")]
    public async Task<ActionResult<OrdenServicioItem>> CambiarEstado(int id, CambiarEstadoOrdenRequest request)
    {
        if (!ESTADOS_VALIDOS.Contains(request.Estado))
            return BadRequest(new { mensaje = "Estado inválido." });

        var orden = await _db.OrdenesServicio.FindAsync(id);
        if (orden == null) return NotFound();

        if (orden.Estado == "Completada" || orden.Estado == "Cancelada")
            return BadRequest(new { mensaje = "Esta orden ya está cerrada, no se puede modificar." });

        bool esTecnicoAsignado = RolActual() == "Tecnico" && orden.TecnicoUsuarioId == UsuarioActualId();
        bool puedeGestionar = await _permisosService.TienePermiso(RolActual(), "OS_GESTIONAR");
        bool puedeActualizarCampo = await _permisosService.TienePermiso(RolActual(), "OS_ACTUALIZAR_CAMPO");

        if (request.Estado == "Cancelada")
        {
            if (!puedeGestionar) return Forbid();
        }
        else
        {
            if (!esTecnicoAsignado && !puedeGestionar) return Forbid();
            if (esTecnicoAsignado && !puedeActualizarCampo) return Forbid();
        }

        orden.Estado = request.Estado;

        if (request.Estado == "Completada")
        {
            orden.FechaCompletada = DateTime.UtcNow;

            if (orden.TecnicoUsuarioId.HasValue)
            {
                var perfil = await _db.PerfilesTecnicos.FirstOrDefaultAsync(p => p.UsuarioId == orden.TecnicoUsuarioId.Value);
                if (perfil == null)
                {
                    perfil = new PerfilTecnico { UsuarioId = orden.TecnicoUsuarioId.Value, TotalServiciosCompletados = 1 };
                    _db.PerfilesTecnicos.Add(perfil);
                }
                else
                {
                    perfil.TotalServiciosCompletados += 1;
                }
            }
        }

        await _auditoria.Registrar("ORDEN_SERVICIO_ESTADO_CAMBIADO", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Orden #{id} -> {request.Estado}");

        await _db.SaveChangesAsync();
        return Ok(await ObtenerItemCompleto(id));
    }

    // -----------------------------------------------------------
    // Agregar un producto usado EN CAMPO: se suma directo a la
    // MISMA venta/boleta de esta orden (descuenta stock ya mismo,
    // actualiza el total de la boleta).
    // -----------------------------------------------------------
    [HttpPost("{id}/productos")]
    public async Task<ActionResult<OrdenServicioItem>> AgregarProducto(int id, AgregarProductoOrdenRequest request)
    {
        var orden = await _db.OrdenesServicio.Include(o => o.Venta).FirstOrDefaultAsync(o => o.Id == id);
        if (orden == null) return NotFound();

        bool esTecnicoAsignado = RolActual() == "Tecnico" && orden.TecnicoUsuarioId == UsuarioActualId();
        bool puedeGestionar = await _permisosService.TienePermiso(RolActual(), "OS_GESTIONAR");
        bool puedeActualizarCampo = await _permisosService.TienePermiso(RolActual(), "OS_ACTUALIZAR_CAMPO");

        if (!puedeGestionar && !(esTecnicoAsignado && puedeActualizarCampo))
            return Forbid();

        if (orden.Estado == "Completada" || orden.Estado == "Cancelada")
            return BadRequest(new { mensaje = "No se pueden agregar productos a una orden cerrada." });

        if (request.Cantidad <= 0)
            return BadRequest(new { mensaje = "La cantidad debe ser mayor a 0." });

        var producto = await _db.Productos.FindAsync(request.ProductoId);
        if (producto == null || !producto.Activo)
            return BadRequest(new { mensaje = "Producto no válido." });

        if (request.Cantidad > producto.Stock)
            return BadRequest(new { mensaje = $"Stock insuficiente de '{producto.Nombre}'. Disponible: {producto.Stock}." });

        var venta = orden.Venta!;

        decimal totalLinea = producto.PrecioUnitario * request.Cantidad;
        decimal subtotalLinea = Math.Round(totalLinea / (1 + TASA_IGV), 2);
        decimal igvLinea = totalLinea - subtotalLinea;

        _db.VentaDetalles.Add(new VentaDetalle
        {
            VentaId = venta.Id,
            ProductoId = producto.Id,
            NombreProducto = producto.Nombre,
            Cantidad = request.Cantidad,
            PrecioUnitario = producto.PrecioUnitario,
            CostoUnitario = producto.CostoUnitario,
            Subtotal = totalLinea,
            AgregadoEnCampo = true
        });

        producto.Stock -= request.Cantidad;

        // Actualiza el total de la MISMA boleta
        venta.SubTotal += subtotalLinea;
        venta.Igv += igvLinea;
        venta.Total += totalLinea;

        await _auditoria.Registrar("PRODUCTO_AGREGADO_EN_CAMPO", UsuarioActualId(), null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Orden #{id} / Venta #{venta.Id} - {producto.Nombre} x{request.Cantidad}");

        await _db.SaveChangesAsync();

        return Ok(await ObtenerItemCompleto(id));
    }

    // -----------------------------------------------------------
    // Quitar un producto agregado en campo por error. Solo se
    // pueden quitar los que se agregaron DESDE la orden (no lo
    // que el cliente ya compró en el mostrador).
    // -----------------------------------------------------------
    [HttpDelete("{id}/productos/{detalleId}")]
    public async Task<ActionResult<OrdenServicioItem>> QuitarProducto(int id, int detalleId)
    {
        var orden = await _db.OrdenesServicio.Include(o => o.Venta).FirstOrDefaultAsync(o => o.Id == id);
        if (orden == null) return NotFound();

        bool esTecnicoAsignado = RolActual() == "Tecnico" && orden.TecnicoUsuarioId == UsuarioActualId();
        bool puedeGestionar = await _permisosService.TienePermiso(RolActual(), "OS_GESTIONAR");
        bool puedeActualizarCampo = await _permisosService.TienePermiso(RolActual(), "OS_ACTUALIZAR_CAMPO");

        if (!puedeGestionar && !(esTecnicoAsignado && puedeActualizarCampo))
            return Forbid();

        if (orden.Estado == "Completada" || orden.Estado == "Cancelada")
            return BadRequest(new { mensaje = "No se pueden quitar productos de una orden cerrada." });

        var detalle = await _db.VentaDetalles.FirstOrDefaultAsync(d => d.Id == detalleId && d.VentaId == orden.VentaId);
        if (detalle == null) return NotFound();

        if (!detalle.AgregadoEnCampo)
            return BadRequest(new { mensaje = "Este producto se compró en el mostrador, no se puede quitar desde la orden." });

        var producto = await _db.Productos.FindAsync(detalle.ProductoId);
        if (producto != null) producto.Stock += detalle.Cantidad;

        decimal subtotalLinea = Math.Round(detalle.Subtotal / (1 + TASA_IGV), 2);
        decimal igvLinea = detalle.Subtotal - subtotalLinea;

        var venta = orden.Venta!;
        venta.SubTotal -= subtotalLinea;
        venta.Igv -= igvLinea;
        venta.Total -= detalle.Subtotal;

        _db.VentaDetalles.Remove(detalle);

        await _db.SaveChangesAsync();
        return Ok(await ObtenerItemCompleto(id));
    }

    // -----------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------
    private async Task<OrdenServicioItem> ObtenerItemCompleto(int id)
    {
        var orden = await _db.OrdenesServicio
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoUsuario)
            .Include(o => o.Venta).ThenInclude(v => v!.Detalles)
            .FirstAsync(o => o.Id == id);

        var creador = await _db.Usuarios.FindAsync(orden.CreadoPorUsuarioId);
        return MapearItem(orden, new Dictionary<int, string> { [orden.CreadoPorUsuarioId] = creador?.NombreCompleto ?? "—" });
    }

    private OrdenServicioItem MapearItem(OrdenServicio o, Dictionary<int, string> creadores)
    {
        var venta = o.Venta;
        return new OrdenServicioItem
        {
            Id = o.Id,
            VentaId = o.VentaId,
            NombreCliente = o.Cliente?.NombreORazonSocial ?? "—",
            DireccionInstalacion = o.DireccionInstalacion,
            NombreTecnico = o.TecnicoUsuario?.NombreCompleto,
            TecnicoUsuarioId = o.TecnicoUsuarioId,
            NombreCreadoPor = creadores.TryGetValue(o.CreadoPorUsuarioId, out var nombre) ? nombre : "—",
            Descripcion = o.Descripcion,
            Estado = o.Estado,
            FechaCreacion = o.FechaCreacion,
            FechaProgramada = o.FechaProgramada,
            FechaCompletada = o.FechaCompletada,
            SubTotal = venta?.SubTotal ?? 0,
            Igv = venta?.Igv ?? 0,
            Total = venta?.Total ?? 0,
            Productos = venta?.Detalles.Select(d => new VentaDetalleItem
            {
                Id = d.Id,
                NombreProducto = d.NombreProducto,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                CostoUnitario = d.CostoUnitario,
                Subtotal = d.Subtotal,
                AgregadoEnCampo = d.AgregadoEnCampo
            }).ToList() ?? new List<VentaDetalleItem>(),
            TokenSeguimiento = o.TokenSeguimiento,
            UbicacionTecnicoLat = o.UbicacionTecnicoLat,
            UbicacionTecnicoLng = o.UbicacionTecnicoLng,
            FechaUltimaUbicacion = o.FechaUltimaUbicacion,
            DestinoLat = o.DestinoLat,
            DestinoLng = o.DestinoLng
        };
    }

    // -----------------------------------------------------------
    // El técnico asignado envía su ubicación GPS actual. Se llama
    // periódicamente desde el celular mientras viaja/trabaja.
    // -----------------------------------------------------------
    [HttpPatch("{id}/ubicacion")]
    public async Task<IActionResult> ActualizarUbicacion(int id, ActualizarUbicacionRequest request)
    {
        var orden = await _db.OrdenesServicio.FindAsync(id);
        if (orden == null) return NotFound();

        bool esTecnicoAsignado = RolActual() == "Tecnico" && orden.TecnicoUsuarioId == UsuarioActualId();
        if (!esTecnicoAsignado)
            return Forbid();

        if (orden.Estado == "Completada" || orden.Estado == "Cancelada")
            return BadRequest(new { mensaje = "Esta orden ya está cerrada." });

        orden.UbicacionTecnicoLat = request.Lat;
        orden.UbicacionTecnicoLng = request.Lng;
        orden.FechaUltimaUbicacion = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Ubicación actualizada." });
    }

    // -----------------------------------------------------------
    // Endpoint PÚBLICO (sin login) para que el cliente vea el
    // estado y la ubicación del técnico usando solo el link que
    // le mandaste por WhatsApp. No expone datos sensibles.
    // -----------------------------------------------------------
    [HttpGet("seguimiento/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<SeguimientoPublicoResponse>> Seguimiento(string token)
    {
        var orden = await _db.OrdenesServicio
            .Include(o => o.Cliente)
            .Include(o => o.TecnicoUsuario)
            .FirstOrDefaultAsync(o => o.TokenSeguimiento == token);

        if (orden == null) return NotFound(new { mensaje = "Link de seguimiento no válido." });

        return Ok(new SeguimientoPublicoResponse
        {
            NombreCliente = orden.Cliente?.NombreORazonSocial ?? "",
            DireccionInstalacion = orden.DireccionInstalacion,
            Estado = orden.Estado,
            NombreTecnico = orden.TecnicoUsuario?.NombreCompleto,
            Lat = orden.UbicacionTecnicoLat,
            Lng = orden.UbicacionTecnicoLng,
            FechaUltimaUbicacion = orden.FechaUltimaUbicacion,
            FechaCreacion = orden.FechaCreacion,
            DestinoLat = orden.DestinoLat,
            DestinoLng = orden.DestinoLng
        });
    }
}