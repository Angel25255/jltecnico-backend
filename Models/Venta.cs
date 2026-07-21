namespace JLTecnico.Auth.Models
{
    public class Venta
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }
        public int VendedorUsuarioId { get; set; }
        public Usuario? VendedorUsuario { get; set; }
        public DateTime FechaVenta { get; set; } = DateTime.UtcNow;
        public decimal SubTotal { get; set; }
        public decimal Igv { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = "Completada"; // Completada, Anulada

        public List<VentaDetalle> Detalles { get; set; } = new();
    }
}
