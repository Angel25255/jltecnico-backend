namespace JLTecnico.Auth.DTOs
{
    public class CotizacionItem
    {
        public int Id { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public string NombreVendedor { get; set; } = string.Empty;
        public DateTime FechaCotizacion { get; set; }
        public DateTime FechaValidez { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Igv { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int? VentaId { get; set; }
        public List<CotizacionDetalleItem> Detalles { get; set; } = new();
    }
}
