namespace JLTecnico.Auth.Models
{
    public class Cotizacion
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }
        public int VendedorUsuarioId { get; set; }
        public Usuario? VendedorUsuario { get; set; }
        public DateTime FechaCotizacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaValidez { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Igv { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = "Pendiente"; // Pendiente, Aprobada, Facturada, Anulada
        public int? VentaId { get; set; }

        public List<CotizacionDetalle> Detalles { get; set; } = new();
    }
}
