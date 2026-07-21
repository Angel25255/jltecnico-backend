namespace JLTecnico.Auth.DTOs
{
    public class VentaItem
    {

        public int Id { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public string NombreVendedor { get; set; } = string.Empty;
        public DateTime FechaVenta { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Igv { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int? OrdenServicioId { get; set; } // si se creó una orden ligada a esta venta
        public List<VentaDetalleItem> Detalles { get; set; } = new();
    }
}
