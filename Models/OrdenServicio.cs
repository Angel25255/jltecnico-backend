namespace JLTecnico.Auth.Models
{
    public class OrdenServicio
    {

        public int Id { get; set; }
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }
        public int VentaId { get; set; }
        public Venta? Venta { get; set; }
        public int? TecnicoUsuarioId { get; set; }
        public Usuario? TecnicoUsuario { get; set; }
        public int CreadoPorUsuarioId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string? DireccionInstalacion { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaProgramada { get; set; }
        public DateTime? FechaCompletada { get; set; }

        // GPS en vivo
        public decimal? UbicacionTecnicoLat { get; set; }
        public decimal? UbicacionTecnicoLng { get; set; }
        public DateTime? FechaUltimaUbicacion { get; set; }
        public string TokenSeguimiento { get; set; } = string.Empty; // usado en el link público, sin login

        // Punto de destino de la instalación (fijo, marcado al crear la orden)
        public decimal? DestinoLat { get; set; }
        public decimal? DestinoLng { get; set; }

    }

}