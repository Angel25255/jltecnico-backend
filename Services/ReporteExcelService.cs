using ClosedXML.Excel;
using JLTecnico.Auth.DTOs;

namespace JLTecnico.Auth.Services;

public class ReporteExcelService
{
    public byte[] GenerarExcel(ReporteCompletoResponse reporte, DateTime desde, DateTime hasta)
    {
        using var libro = new XLWorkbook();

        // ---- Hoja 1: Resumen ----
        var hojaResumen = libro.Worksheets.Add("Resumen");
        hojaResumen.Cell(1, 1).Value = "REPORTE COMERCIAL - JL TÉCNICO EIRL";
        hojaResumen.Cell(1, 1).Style.Font.Bold = true;
        hojaResumen.Cell(1, 1).Style.Font.FontSize = 14;
        hojaResumen.Cell(2, 1).Value = $"Periodo: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";

        var filasResumen = new (string, object)[]
        {
            ("Total vendido", reporte.Resumen.TotalVendido),
            ("Total comprado (a proveedores)", reporte.Resumen.TotalComprado),
            ("Ganancia total (venta - costo)", reporte.Resumen.GananciaTotal),
            ("Cantidad de ventas", reporte.Resumen.CantidadVentas),
            ("Ticket promedio", reporte.Resumen.TicketPromedio),
            ("Ventas anuladas", reporte.Resumen.VentasAnuladas),
            ("Cotizaciones pendientes", reporte.Resumen.CotizacionesPendientes),
            ("Cotizaciones aprobadas", reporte.Resumen.CotizacionesAprobadas),
            ("Monto cotizado pendiente", reporte.Resumen.MontoCotizadoPendiente),
            ("Productos con stock bajo", reporte.Resumen.ProductosStockBajo),
        };

        int fila = 4;
        foreach (var (etiqueta, valor) in filasResumen)
        {
            hojaResumen.Cell(fila, 1).Value = etiqueta;
            hojaResumen.Cell(fila, 1).Style.Font.Bold = true;
            hojaResumen.Cell(fila, 2).Value = XLCellValue.FromObject(valor);
            fila++;
        }
        hojaResumen.Columns().AdjustToContents();

        // ---- Hoja 2: Ventas por producto ----
        var hojaProductos = libro.Worksheets.Add("Ventas por Producto");
        hojaProductos.Cell(1, 1).Value = "Producto";
        hojaProductos.Cell(1, 2).Value = "Código";
        hojaProductos.Cell(1, 3).Value = "Cantidad vendida";
        hojaProductos.Cell(1, 4).Value = "Total vendido (S/)";
        hojaProductos.Cell(1, 5).Value = "Costo total (S/)";
        hojaProductos.Cell(1, 6).Value = "Ganancia (S/)";
        hojaProductos.Range(1, 1, 1, 6).Style.Font.Bold = true;
        hojaProductos.Range(1, 1, 1, 6).Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
        hojaProductos.Range(1, 1, 1, 6).Style.Font.FontColor = XLColor.White;

        int f = 2;
        foreach (var p in reporte.VentasPorProducto)
        {
            hojaProductos.Cell(f, 1).Value = p.NombreProducto;
            hojaProductos.Cell(f, 2).Value = p.Codigo ?? "";
            hojaProductos.Cell(f, 3).Value = p.CantidadVendida;
            hojaProductos.Cell(f, 4).Value = p.TotalVendido;
            hojaProductos.Cell(f, 5).Value = p.CostoTotal;
            hojaProductos.Cell(f, 6).Value = p.Ganancia;
            f++;
        }
        hojaProductos.Columns().AdjustToContents();

        // ---- Hoja 3: Ventas por cliente ----
        var hojaClientes = libro.Worksheets.Add("Ventas por Cliente");
        hojaClientes.Cell(1, 1).Value = "Cliente";
        hojaClientes.Cell(1, 2).Value = "Documento";
        hojaClientes.Cell(1, 3).Value = "N° de compras";
        hojaClientes.Cell(1, 4).Value = "Total comprado (S/)";
        hojaClientes.Range(1, 1, 1, 4).Style.Font.Bold = true;
        hojaClientes.Range(1, 1, 1, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
        hojaClientes.Range(1, 1, 1, 4).Style.Font.FontColor = XLColor.White;

        f = 2;
        foreach (var c in reporte.VentasPorCliente)
        {
            hojaClientes.Cell(f, 1).Value = c.NombreCliente;
            hojaClientes.Cell(f, 2).Value = c.NumeroDocumento;
            hojaClientes.Cell(f, 3).Value = c.CantidadCompras;
            hojaClientes.Cell(f, 4).Value = c.TotalComprado;
            f++;
        }
        hojaClientes.Columns().AdjustToContents();

        // ---- Hoja 4: Stock bajo ----
        var hojaStock = libro.Worksheets.Add("Stock Bajo");
        hojaStock.Cell(1, 1).Value = "Producto";
        hojaStock.Cell(1, 2).Value = "Código";
        hojaStock.Cell(1, 3).Value = "Categoría";
        hojaStock.Cell(1, 4).Value = "Stock actual";
        hojaStock.Range(1, 1, 1, 4).Style.Font.Bold = true;
        hojaStock.Range(1, 1, 1, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(185, 28, 28);
        hojaStock.Range(1, 1, 1, 4).Style.Font.FontColor = XLColor.White;

        f = 2;
        foreach (var p in reporte.ProductosStockBajo)
        {
            hojaStock.Cell(f, 1).Value = p.Nombre;
            hojaStock.Cell(f, 2).Value = p.Codigo ?? "";
            hojaStock.Cell(f, 3).Value = p.NombreCategoria ?? "";
            hojaStock.Cell(f, 4).Value = p.Stock;
            f++;
        }
        hojaStock.Columns().AdjustToContents();

        // ---- Hoja 5: Cotizaciones pendientes ----
        var hojaCotizaciones = libro.Worksheets.Add("Cotizaciones Pendientes");
        hojaCotizaciones.Cell(1, 1).Value = "N°";
        hojaCotizaciones.Cell(1, 2).Value = "Cliente";
        hojaCotizaciones.Cell(1, 3).Value = "Total (S/)";
        hojaCotizaciones.Cell(1, 4).Value = "Fecha emisión";
        hojaCotizaciones.Cell(1, 5).Value = "Válida hasta";
        hojaCotizaciones.Cell(1, 6).Value = "Días restantes";
        hojaCotizaciones.Cell(1, 7).Value = "Estado";
        hojaCotizaciones.Range(1, 1, 1, 7).Style.Font.Bold = true;
        hojaCotizaciones.Range(1, 1, 1, 7).Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
        hojaCotizaciones.Range(1, 1, 1, 7).Style.Font.FontColor = XLColor.White;

        f = 2;
        foreach (var c in reporte.CotizacionesPendientes)
        {
            hojaCotizaciones.Cell(f, 1).Value = c.Id;
            hojaCotizaciones.Cell(f, 2).Value = c.NombreCliente;
            hojaCotizaciones.Cell(f, 3).Value = c.Total;
            hojaCotizaciones.Cell(f, 4).Value = c.FechaCotizacion;
            hojaCotizaciones.Cell(f, 5).Value = c.FechaValidez;
            hojaCotizaciones.Cell(f, 6).Value = c.DiasRestantes;
            hojaCotizaciones.Cell(f, 7).Value = c.Estado;
            f++;
        }
        hojaCotizaciones.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }
}