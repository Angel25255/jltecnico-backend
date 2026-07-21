namespace JLTecnico.Auth.DTOs
{
    public class CotizacionPendienteItem
    {
        public int Id { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime FechaCotizacion { get; set; }
        public DateTime FechaValidez { get; set; }
        public int DiasRestantes { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
