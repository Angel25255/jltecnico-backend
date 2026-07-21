namespace JLTecnico.Auth.DTOs
{
    public class CrearOrdenServicioRequest
    {
        public int VentaId { get; set; } // obligatorio: la venta del mostrador que origina esta orden
        public int? TecnicoUsuarioId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string? DireccionInstalacion { get; set; }
        public DateTime? FechaProgramada { get; set; }
        public decimal? DestinoLat { get; set; } // punto exacto marcado en el mapa
        public decimal? DestinoLng { get; set; }
    }

    }

