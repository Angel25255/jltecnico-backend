using JLTecnico.Auth.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JLTecnico.Auth.Services;

// Genera el PDF de la cotización PARA ENTREGAR AL CLIENTE.
// A propósito NO muestra el precio unitario de cada producto
// (solo cantidad y el total de esa línea) para que el cliente
// no pueda comparar precios unitarios con la competencia.
public class CotizacionPdfService
{
    public byte[] GenerarCotizacionPdf(Cotizacion cotizacion)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("JL TÉCNICO EIRL").FontSize(16).Bold();
                            c.Item().Text("Venta de productos y servicio técnico");
                            c.Item().Text("Huancayo, Perú");
                        });

                        row.ConstantItem(170).Border(1).BorderColor(Colors.Grey.Medium)
                            .Padding(8).Column(c =>
                            {
                                c.Item().AlignCenter().Text("COTIZACIÓN").Bold();
                                c.Item().AlignCenter().Text($"N° {cotizacion.Id:D6}").FontSize(13).Bold();
                                c.Item().AlignCenter().Text($"Válida hasta: {cotizacion.FechaValidez:dd/MM/yyyy}").FontSize(8);
                            });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Cliente: ").Bold(); t.Span(cotizacion.Cliente?.NombreORazonSocial ?? "—"); });
                            c.Item().Text(t => { t.Span("Documento: ").Bold(); t.Span($"{cotizacion.Cliente?.TipoDocumento} {cotizacion.Cliente?.NumeroDocumento}"); });
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignRight().Text(t => { t.Span("Fecha: ").Bold(); t.Span(cotizacion.FechaCotizacion.ToString("dd/MM/yyyy")); });
                            c.Item().AlignRight().Text(t => { t.Span("Atendido por: ").Bold(); t.Span(cotizacion.VendedorUsuario?.NombreCompleto ?? "—"); });
                        });
                    });

                    col.Item().PaddingTop(15);

                    // Tabla SIN precio unitario: solo Producto, Cantidad, Total de línea
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.8f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CeldaEncabezado).Text("Producto");
                            header.Cell().Element(CeldaEncabezado).AlignCenter().Text("Cant.");
                            header.Cell().Element(CeldaEncabezado).AlignRight().Text("Total");

                            static IContainer CeldaEncabezado(IContainer c) =>
                                c.DefaultTextStyle(x => x.Bold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
                        });

                        foreach (var detalle in cotizacion.Detalles)
                        {
                            table.Cell().Element(CeldaCuerpo).Text(detalle.NombreProducto);
                            table.Cell().Element(CeldaCuerpo).AlignCenter().Text(detalle.Cantidad.ToString());
                            table.Cell().Element(CeldaCuerpo).AlignRight().Text($"S/ {detalle.Subtotal:F2}");

                            static IContainer CeldaCuerpo(IContainer c) =>
                                c.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        }
                    });

                    col.Item().PaddingTop(15).AlignRight().Column(c =>
                    {
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("TOTAL:").Bold().FontSize(13);
                            r.ConstantItem(100).AlignRight().Text($"S/ {cotizacion.Total:F2}").Bold().FontSize(13);
                        });
                        c.Item().AlignRight().Text("(Precio incluye IGV)").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    if (cotizacion.Estado == "Anulada")
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("*** COTIZACIÓN ANULADA ***")
                            .FontColor(Colors.Red.Medium).Bold().FontSize(14);
                    }

                    col.Item().PaddingTop(25).Column(c =>
                    {
                        c.Item().Text("Condiciones:").Bold().FontSize(9);
                        c.Item().Text($"• Esta cotización es válida hasta el {cotizacion.FechaValidez:dd/MM/yyyy}.").FontSize(8.5f);
                        c.Item().Text("• Precios sujetos a disponibilidad de stock al momento de confirmar la compra.").FontSize(8.5f);
                    });
                });

                page.Footer().AlignCenter().Text("JL Técnico EIRL — Gracias por su preferencia").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        return documento.GeneratePdf();
    }
}