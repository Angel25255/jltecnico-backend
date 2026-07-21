using System.Security.Claims;
using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReportesController : ControllerBase
{
    private const int UMBRAL_STOCK_BAJO = 5;

    private readonly AppDbContext _db;
    private readonly PermisosService _permisosService;
    private readonly ReportePdfService _pdfService;
    private readonly ReporteExcelService _excelService;

    public ReportesController(AppDbContext db, PermisosService permisosService,
        ReportePdfService pdfService, ReporteExcelService excelService)
    {
        _db = db;
        _permisosService = permisosService;
        _pdfService = pdfService;
        _excelService = excelService;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    // -----------------------------------------------------------
    // Perú está en UTC-5. Las fechas se guardan en UTC en la BD,
    // pero "el día de hoy" para el negocio debe calcularse en hora
    // de Perú, no en UTC (si no, cerca de la medianoche el reporte
    // "salta" al día siguiente antes de tiempo).
    // -----------------------------------------------------------
    private static readonly TimeZoneInfo ZonaPeru = ObtenerZonaPeru();

    private static TimeZoneInfo ObtenerZonaPeru()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); } // Windows
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Lima"); } // Linux/Mac
            catch { return TimeZoneInfo.CreateCustomTimeZone("Peru", TimeSpan.FromHours(-5), "Perú", "Perú"); }
        }
    }

    private static DateTime AUtc(DateTime fechaLocalPeru) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fechaLocalPeru, DateTimeKind.Unspecified), ZonaPeru);

    private static DateTime AHoraPeru(DateTime fechaUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc), ZonaPeru);

    private static DateTime HoyEnPeru() => AHoraPeru(DateTime.UtcNow).Date;

    private string RolActualSeguro() => RolActual();

    // -----------------------------------------------------------
    // GET /api/reportes/completo?desde=2026-07-01&hasta=2026-07-18
    // "desde" y "hasta" se interpretan como fechas de Perú.
    // -----------------------------------------------------------
    [HttpGet("completo")]
    public async Task<ActionResult<ReporteCompletoResponse>> ReporteCompleto(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "REPORTES_VER"))
            return Forbid();

        var (desdeUtc, hastaUtc) = CalcularRangoUtc(desde, hasta);
        var reporte = await ConstruirReporte(desdeUtc, hastaUtc);
        return Ok(reporte);
    }

    // Convierte el rango de fechas (interpretado en hora de Perú) al
    // rango UTC equivalente para consultar la base de datos.
    private (DateTime desdeUtc, DateTime hastaUtc) CalcularRangoUtc(DateTime? desde, DateTime? hasta)
    {
        DateTime hoyPeru = HoyEnPeru();
        DateTime fechaDesdeLocal = (desde ?? hoyPeru.AddDays(-30)).Date;
        DateTime fechaHastaLocal = (hasta ?? hoyPeru).Date;

        DateTime desdeUtc = AUtc(fechaDesdeLocal);                                  // 00:00 de Perú ese día
        DateTime hastaUtc = AUtc(fechaHastaLocal.AddDays(1).AddTicks(-1));           // 23:59:59.999 de Perú ese día

        return (desdeUtc, hastaUtc);
    }

    private async Task<ReporteCompletoResponse> ConstruirReporte(DateTime desdeUtc, DateTime hastaUtc)
    {
        var ventas = await _db.Ventas
            .Include(v => v.Cliente)
            .Include(v => v.Detalles)
            .Where(v => v.FechaVenta >= desdeUtc && v.FechaVenta <= hastaUtc)
            .ToListAsync();

        var ventasCompletadas = ventas.Where(v => v.Estado == "Completada").ToList();
        var ventasAnuladas = ventas.Where(v => v.Estado == "Anulada").ToList();

        var cotizaciones = await _db.Cotizaciones
            .Include(c => c.Cliente)
            .Where(c => c.FechaCotizacion >= desdeUtc && c.FechaCotizacion <= hastaUtc)
            .ToListAsync();

        var compras = await _db.Compras
            .Where(c => c.FechaCompra >= desdeUtc && c.FechaCompra <= hastaUtc)
            .ToListAsync();

        // ---- Resumen general ----
        decimal totalVendido = ventasCompletadas.Sum(v => v.Total);
        int cantidadVentas = ventasCompletadas.Count;
        decimal ticketPromedio = cantidadVentas > 0 ? Math.Round(totalVendido / cantidadVentas, 2) : 0;
        decimal totalComprado = compras.Sum(c => c.Total);

        // Ganancia = (precio de venta - costo) x cantidad, por cada línea vendida.
        // El costo queda "congelado" en cada línea al momento de la venta.
        decimal gananciaTotal = ventasCompletadas
            .SelectMany(v => v.Detalles)
            .Sum(d => (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad);

        var pendientes = cotizaciones.Where(c => c.Estado == "Pendiente").ToList();
        var aprobadas = cotizaciones.Where(c => c.Estado == "Aprobada").ToList();

        var productosStockBajo = await _db.Productos
            .Include(p => p.Categoria)
            .Where(p => p.Activo && p.Stock <= UMBRAL_STOCK_BAJO)
            .OrderBy(p => p.Stock)
            .ToListAsync();

        var resumen = new ResumenGeneralResponse
        {
            TotalVendido = totalVendido,
            CantidadVentas = cantidadVentas,
            TicketPromedio = ticketPromedio,
            CotizacionesPendientes = pendientes.Count,
            CotizacionesAprobadas = aprobadas.Count,
            MontoCotizadoPendiente = pendientes.Sum(c => c.Total),
            ProductosStockBajo = productosStockBajo.Count,
            VentasAnuladas = ventasAnuladas.Count,
            GananciaTotal = Math.Round(gananciaTotal, 2),
            TotalComprado = totalComprado
        };

        // ---- Ventas por periodo (agrupado por día EN HORA DE PERÚ) ----
        var ventasPorPeriodo = ventasCompletadas
            .GroupBy(v => AHoraPeru(v.FechaVenta).Date)
            .OrderBy(g => g.Key)
            .Select(g => new VentaPorPeriodoItem
            {
                Periodo = g.Key.ToString("dd/MM"),
                Total = g.Sum(v => v.Total),
                CantidadVentas = g.Count()
            })
            .ToList();

        // ---- Ventas por producto ----
        var ventasPorProducto = ventasCompletadas
            .SelectMany(v => v.Detalles)
            .GroupBy(d => d.NombreProducto)
            .Select(g => new VentaPorProductoItem
            {
                NombreProducto = g.Key,
                CantidadVendida = g.Sum(d => d.Cantidad),
                TotalVendido = g.Sum(d => d.Subtotal),
                CostoTotal = g.Sum(d => d.CostoUnitario * d.Cantidad),
                Ganancia = g.Sum(d => (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad)
            })
            .OrderByDescending(p => p.TotalVendido)
            .Take(10)
            .ToList();

        // ---- Ventas por cliente ----
        var ventasPorCliente = ventasCompletadas
            .Where(v => v.Cliente != null)
            .GroupBy(v => new { v.ClienteId, Nombre = v.Cliente!.NombreORazonSocial, Doc = v.Cliente.NumeroDocumento })
            .Select(g => new VentaPorClienteItem
            {
                NombreCliente = g.Key.Nombre,
                NumeroDocumento = g.Key.Doc,
                CantidadCompras = g.Count(),
                TotalComprado = g.Sum(v => v.Total)
            })
            .OrderByDescending(c => c.TotalComprado)
            .Take(10)
            .ToList();

        // ---- Stock bajo ----
        var stockBajoItems = productosStockBajo.Select(p => new ProductoStockBajoItem
        {
            Nombre = p.Nombre,
            Codigo = p.Codigo,
            NombreCategoria = p.Categoria?.Nombre,
            Stock = p.Stock
        }).ToList();

        // ---- Cotizaciones pendientes (días restantes calculados en hora de Perú) ----
        DateTime hoyPeru = HoyEnPeru();
        var cotizacionesPendientesItems = cotizaciones
            .Where(c => c.Estado == "Pendiente" || c.Estado == "Aprobada")
            .OrderBy(c => c.FechaValidez)
            .Select(c => new CotizacionPendienteItem
            {
                Id = c.Id,
                NombreCliente = c.Cliente?.NombreORazonSocial ?? "—",
                Total = c.Total,
                FechaCotizacion = AHoraPeru(c.FechaCotizacion),
                FechaValidez = AHoraPeru(c.FechaValidez),
                DiasRestantes = (int)(AHoraPeru(c.FechaValidez).Date - hoyPeru).TotalDays,
                Estado = c.Estado
            })
            .ToList();

        return new ReporteCompletoResponse
        {
            Resumen = resumen,
            VentasPorPeriodo = ventasPorPeriodo,
            VentasPorProducto = ventasPorProducto,
            VentasPorCliente = ventasPorCliente,
            ProductosStockBajo = stockBajoItems,
            CotizacionesPendientes = cotizacionesPendientesItems
        };
    }

    // -----------------------------------------------------------
    // Exportar a Excel
    // -----------------------------------------------------------
    [HttpGet("excel")]
    public async Task<IActionResult> ExportarExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "REPORTES_VER"))
            return Forbid();

        var (desdeUtc, hastaUtc) = CalcularRangoUtc(desde, hasta);
        var reporte = await ConstruirReporte(desdeUtc, hastaUtc);
        byte[] excelBytes = _excelService.GenerarExcel(reporte, AHoraPeru(desdeUtc), AHoraPeru(hastaUtc));

        return File(excelBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Reporte-Comercial-{HoyEnPeru():yyyyMMdd}.xlsx");
    }

    // -----------------------------------------------------------
    // Exportar a PDF
    // -----------------------------------------------------------
    [HttpGet("pdf")]
    public async Task<IActionResult> ExportarPdf([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "REPORTES_VER"))
            return Forbid();

        var (desdeUtc, hastaUtc) = CalcularRangoUtc(desde, hasta);
        var reporte = await ConstruirReporte(desdeUtc, hastaUtc);
        byte[] pdfBytes = _pdfService.GenerarReportePdf(reporte, AHoraPeru(desdeUtc), AHoraPeru(hastaUtc));

        return File(pdfBytes, "application/pdf", $"Reporte-Comercial-{HoyEnPeru():yyyyMMdd}.pdf");
    }
}