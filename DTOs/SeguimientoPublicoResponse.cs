namespace JLTecnico.Auth.DTOs
{
    public class SeguimientoPublicoResponse
    {
        public string NombreCliente { get; set; } = string.Empty;
        public string? DireccionInstalacion { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? NombreTecnico { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public DateTime? FechaUltimaUbicacion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public decimal? DestinoLat { get; set; }
        public decimal? DestinoLng { get; set; }
    }
}
