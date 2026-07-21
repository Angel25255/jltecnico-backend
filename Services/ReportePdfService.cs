using JLTecnico.Auth.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JLTecnico.Auth.Services;

public class ReportePdfService
{
    public byte[] GenerarReportePdf(ReporteCompletoResponse reporte, DateTime desde, DateTime hasta)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("JL TÉCNICO EIRL — Reporte Comercial").FontSize(16).Bold();
                    col.Item().Text($"Periodo: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    // ---- KPIs ----
                    col.Item().Row(row =>
                    {
                        Kpi(row, "Total Vendido", $"S/ {reporte.Resumen.TotalVendido:F2}");
                        Kpi(row, "Total Comprado", $"S/ {reporte.Resumen.TotalComprado:F2}");
                        Kpi(row, "Ganancia", $"S/ {reporte.Resumen.GananciaTotal:F2}");
                        Kpi(row, "N° Ventas", reporte.Resumen.CantidadVentas.ToString());
                        Kpi(row, "Stock Bajo", reporte.Resumen.ProductosStockBajo.ToString());
                    });

                    col.Item().PaddingTop(16).Text("Top 10 productos más vendidos").Bold().FontSize(11);
                    col.Item().PaddingTop(4);

                    decimal maxProducto = reporte.VentasPorProducto.Count > 0
                        ? reporte.VentasPorProducto.Max(p => p.TotalVendido) : 1;
                    if (maxProducto == 0) maxProducto = 1;

                    foreach (var p in reporte.VentasPorProducto)
                    {
                        col.Item().PaddingBottom(4).Row(r =>
                        {
                            r.ConstantItem(150).Text(p.NombreProducto).FontSize(8.5f);
                            r.RelativeItem().Height(14).Background(Colors.Grey.Lighten3).Row(barra =>
                            {
                                float porcentaje = (float)(p.TotalVendido / maxProducto);
                                barra.RelativeItem(porcentaje).Background(Colors.Amber.Medium);
                                barra.RelativeItem(1 - porcentaje);
                            });
                            r.ConstantItem(70).AlignRight().Text($"S/ {p.TotalVendido:F2}").FontSize(8f).Bold();
                            r.ConstantItem(65).AlignRight().Text($"Gan: S/ {p.Ganancia:F2}").FontSize(7.5f).FontColor(Colors.Green.Darken2);
                        });
                    }

                    // ---- Ventas por cliente ----
                    col.Item().PaddingTop(18).Text("Top clientes").Bold().FontSize(11);
                    col.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("Cliente");
                            h.Cell().Element(Encabezado).Text("Documento");
                            h.Cell().Element(Encabezado).AlignCenter().Text("Compras");
                            h.Cell().Element(Encabezado).AlignRight().Text("Total (S/)");
                        });

                        foreach (var c in reporte.VentasPorCliente)
                        {
                            table.Cell().Element(Cuerpo).Text(c.NombreCliente);
                            table.Cell().Element(Cuerpo).Text(c.NumeroDocumento);
                            table.Cell().Element(Cuerpo).AlignCenter().Text(c.CantidadCompras.ToString());
                            table.Cell().Element(Cuerpo).AlignRight().Text(c.TotalComprado.ToString("F2"));
                        }
                    });

                    // ---- Stock bajo ----
                    col.Item().PaddingTop(18).Text("⚠ Productos con stock bajo").Bold().FontSize(11).FontColor(Colors.Red.Medium);
                    col.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1.5f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("Producto");
                            h.Cell().Element(Encabezado).Text("Código");
                            h.Cell().Element(Encabezado).Text("Categoría");
                            h.Cell().Element(Encabezado).AlignCenter().Text("Stock");
                        });

                        foreach (var p in reporte.ProductosStockBajo)
                        {
                            table.Cell().Element(Cuerpo).Text(p.Nombre);
                            table.Cell().Element(Cuerpo).Text(p.Codigo ?? "—");
                            table.Cell().Element(Cuerpo).Text(p.NombreCategoria ?? "—");
                            table.Cell().Element(Cuerpo).AlignCenter().Text(p.Stock.ToString()).FontColor(Colors.Red.Medium).Bold();
                        }
                    });

                    // ---- Cotizaciones pendientes ----
                    col.Item().PaddingTop(18).Text("Cotizaciones pendientes de seguimiento").Bold().FontSize(11);
                    col.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1.5f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("N°");
                            h.Cell().Element(Encabezado).Text("Cliente");
                            h.Cell().Element(Encabezado).AlignRight().Text("Total (S/)");
                            h.Cell().Element(Encabezado).Text("Válida hasta");
                            h.Cell().Element(Encabezado).AlignCenter().Text("Días rest.");
                        });

                        foreach (var c in reporte.CotizacionesPendientes)
                        {
                            table.Cell().Element(Cuerpo).Text(c.Id.ToString());
                            table.Cell().Element(Cuerpo).Text(c.NombreCliente);
                            table.Cell().Element(Cuerpo).AlignRight().Text(c.Total.ToString("F2"));
                            table.Cell().Element(Cuerpo).Text(c.FechaValidez.ToString("dd/MM/yyyy"));
                            table.Cell().Element(Cuerpo).AlignCenter()
                                .Text(c.DiasRestantes.ToString())
                                .FontColor(c.DiasRestantes <= 3 ? Colors.Red.Medium : Colors.Black);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("JL Técnico EIRL — Reporte generado el ").FontSize(7.5f);
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7.5f);
                });
            });
        });

        return documento.GeneratePdf();

        static void Kpi(RowDescriptor row, string etiqueta, string valor)
        {
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
            {
                c.Item().Text(etiqueta).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                c.Item().Text(valor).FontSize(12).Bold();
            });
        }

        static IContainer Encabezado(IContainer c) =>
            c.DefaultTextStyle(x => x.Bold().FontSize(8.5f)).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Darken1);

        static IContainer Cuerpo(IContainer c) =>
            c.DefaultTextStyle(x => x.FontSize(8.5f)).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
    }
}