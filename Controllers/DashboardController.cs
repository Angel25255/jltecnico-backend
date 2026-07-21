using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

// Panel de Control Gerencial: junta KPIs de Ventas, Cotizaciones,
// Órdenes de Servicio, Técnicos e Inventario en una sola pantalla.
// Solo el Administrador lo ve (es la pantalla de inicio de su rol).
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Administrador")]
public class DashboardController : ControllerBase
{
    private const int UMBRAL_STOCK_BAJO = 5;
    private static readonly TimeZoneInfo ZonaPeru = ObtenerZonaPeru();

    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    private static TimeZoneInfo ObtenerZonaPeru()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Lima"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("Peru", TimeSpan.FromHours(-5), "Perú", "Perú"); }
        }
    }

    private static DateTime AUtc(DateTime fechaLocalPeru) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fechaLocalPeru, DateTimeKind.Unspecified), ZonaPeru);

    private static DateTime AHoraPeru(DateTime fechaUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc), ZonaPeru);

    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenDashboardResponse>> Resumen()
    {
        DateTime hoyPeru = AHoraPeru(DateTime.UtcNow).Date;
        DateTime inicioHoyUtc = AUtc(hoyPeru);
        DateTime finHoyUtc = AUtc(hoyPeru.AddDays(1).AddTicks(-1));

        DateTime inicioMesPeru = new DateTime(hoyPeru.Year, hoyPeru.Month, 1);
        DateTime inicioMesUtc = AUtc(inicioMesPeru);

        DateTime inicio7DiasUtc = AUtc(hoyPeru.AddDays(-6));

        var ventasMes = await _db.Ventas
            .Include(v => v.Detalles)
            .Where(v => v.FechaVenta >= inicioMesUtc && v.Estado == "Completada")
            .ToListAsync();

        var ventasHoy = ventasMes.Where(v => v.FechaVenta >= inicioHoyUtc && v.FechaVenta <= finHoyUtc).ToList();

        decimal gananciaMes = ventasMes
            .SelectMany(v => v.Detalles)
            .Sum(d => (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad);

        var ventasUltimos7Dias = await _db.Ventas
            .Where(v => v.FechaVenta >= inicio7DiasUtc && v.Estado == "Completada")
            .ToListAsync();

        var ventasPorDia = ventasUltimos7Dias
            .GroupBy(v => AHoraPeru(v.FechaVenta).Date)
            .Select(g => new VentaDiaItem { Dia = g.Key.ToString("dd/MM"), Total = g.Sum(v => v.Total) })
            .OrderBy(v => v.Dia)
            .ToList();

        var topProductosMes = ventasMes
            .SelectMany(v => v.Detalles)
            .GroupBy(d => d.NombreProducto)
            .Select(g => new TopProductoDashboardItem { Nombre = g.Key, Total = g.Sum(d => d.Subtotal) })
            .OrderByDescending(p => p.Total)
            .Take(5)
            .ToList();

        var cotizacionesPendientes = await _db.Cotizaciones.Where(c => c.Estado == "Pendiente").ToListAsync();

        var ordenesActivas = await _db.OrdenesServicio
            .Where(o => o.Estado != "Completada" && o.Estado != "Cancelada")
            .ToListAsync();

        var ordenesPorEstado = new OrdenesActivasPorEstado
        {
            Pendiente = ordenesActivas.Count(o => o.Estado == "Pendiente"),
            Asignada = ordenesActivas.Count(o => o.Estado == "Asignada"),
            EnCamino = ordenesActivas.Count(o => o.Estado == "EnCamino"),
            EnProceso = ordenesActivas.Count(o => o.Estado == "EnProceso"),
        };

        var tecnicos = await _db.Usuarios.Where(u => u.Rol == "Tecnico" && u.Activo).ToListAsync();
        var perfilesTecnicos = await _db.PerfilesTecnicos
            .Where(p => tecnicos.Select(t => t.Id).Contains(p.UsuarioId))
            .ToListAsync();
        int tecnicosDisponibles = tecnicos.Count(t =>
        {
            var perfil = perfilesTecnicos.FirstOrDefault(p => p.UsuarioId == t.Id);
            return (perfil?.EstadoDisponibilidad ?? "Disponible") == "Disponible";
        });

        int productosStockBajo = await _db.Productos.CountAsync(p => p.Activo && p.Stock <= UMBRAL_STOCK_BAJO);

        return Ok(new ResumenDashboardResponse
        {
            VentasHoyTotal = ventasHoy.Sum(v => v.Total),
            VentasHoyCantidad = ventasHoy.Count,
            VentasMesTotal = ventasMes.Sum(v => v.Total),
            VentasMesCantidad = ventasMes.Count,
            GananciaMes = Math.Round(gananciaMes, 2),
            CotizacionesPendientes = cotizacionesPendientes.Count,
            MontoCotizadoPendiente = cotizacionesPendientes.Sum(c => c.Total),
            OrdenesActivas = ordenesPorEstado,
            TecnicosDisponibles = tecnicosDisponibles,
            TecnicosTotal = tecnicos.Count,
            ProductosStockBajo = productosStockBajo,
            VentasUltimos7Dias = ventasPorDia,
            TopProductosMes = topProductosMes
        });
    }
}