using JLTecnico.Auth.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JLTecnico.Auth.Services;

// Genera el PDF de la boleta de venta. Formato simple tipo A4,
// pensado para enviar por correo o guardar - no es el formato
// de ticket térmico (58mm/80mm), ese se arma aparte.
public class BoletaPdfService
{
    public byte[] GenerarBoletaPdf(Venta venta)
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

                        row.ConstantItem(160).Border(1).BorderColor(Colors.Grey.Medium)
                            .Padding(8).Column(c =>
                            {
                                c.Item().AlignCenter().Text("BOLETA DE VENTA").Bold();
                                c.Item().AlignCenter().Text($"N° {venta.Id:D6}").FontSize(13).Bold();
                            });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    // Datos del cliente y la venta
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Cliente: ").Bold(); t.Span(venta.Cliente?.NombreORazonSocial ?? "—"); });
                            c.Item().Text(t => { t.Span("Documento: ").Bold(); t.Span($"{venta.Cliente?.TipoDocumento} {venta.Cliente?.NumeroDocumento}"); });
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignRight().Text(t => { t.Span("Fecha: ").Bold(); t.Span(venta.FechaVenta.ToString("dd/MM/yyyy HH:mm")); });
                            c.Item().AlignRight().Text(t => { t.Span("Vendedor: ").Bold(); t.Span(venta.VendedorUsuario?.NombreCompleto ?? "—"); });
                        });
                    });

                    col.Item().PaddingTop(15);

                    // Tabla de productos
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CeldaEncabezado).Text("Producto");
                            header.Cell().Element(CeldaEncabezado).AlignCenter().Text("Cant.");
                            header.Cell().Element(CeldaEncabezado).AlignRight().Text("P. Unit.");
                            header.Cell().Element(CeldaEncabezado).AlignRight().Text("Subtotal");

                            static IContainer CeldaEncabezado(IContainer c) =>
                                c.DefaultTextStyle(x => x.Bold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
                        });

                        foreach (var detalle in venta.Detalles)
                        {
                            table.Cell().Element(CeldaCuerpo).Text(detalle.NombreProducto);
                            table.Cell().Element(CeldaCuerpo).AlignCenter().Text(detalle.Cantidad.ToString());
                            table.Cell().Element(CeldaCuerpo).AlignRight().Text($"S/ {detalle.PrecioUnitario:F2}");
                            table.Cell().Element(CeldaCuerpo).AlignRight().Text($"S/ {detalle.Subtotal:F2}");

                            static IContainer CeldaCuerpo(IContainer c) =>
                                c.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        }
                    });

                    col.Item().PaddingTop(15).AlignRight().Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("Subtotal:");
                            r.ConstantItem(90).AlignRight().Text($"S/ {venta.SubTotal:F2}");
                        });
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("IGV (18%):");
                            r.ConstantItem(90).AlignRight().Text($"S/ {venta.Igv:F2}");
                        });
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("TOTAL:").Bold().FontSize(12);
                            r.ConstantItem(90).AlignRight().Text($"S/ {venta.Total:F2}").Bold().FontSize(12);
                        });
                    });

                    if (venta.Estado == "Anulada")
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("*** VENTA ANULADA ***")
                            .FontColor(Colors.Red.Medium).Bold().FontSize(14);
                    }
                });

                page.Footer().AlignCenter().Text("Gracias por su compra — JL Técnico EIRL").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        return documento.GeneratePdf();
    }
}