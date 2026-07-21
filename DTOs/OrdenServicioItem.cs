namespace JLTecnico.Auth.DTOs
{
    public class OrdenServicioItem
    {
        public int Id { get; set; }
        public int VentaId { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public string? DireccionInstalacion { get; set; }
        public string? NombreTecnico { get; set; }
        public int? TecnicoUsuarioId { get; set; }
        public string NombreCreadoPor { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaProgramada { get; set; }
        public DateTime? FechaCompletada { get; set; }

        // Todo esto viene DIRECTO de la venta ligada (una sola boleta,
        // que crece si se agrega algo en campo)
        public decimal SubTotal { get; set; }
        public decimal Igv { get; set; }
        public decimal Total { get; set; }
        public List<VentaDetalleItem> Productos { get; set; } = new();

        // GPS en vivo
        public string TokenSeguimiento { get; set; } = string.Empty;
        public decimal? UbicacionTecnicoLat { get; set; }
        public decimal? UbicacionTecnicoLng { get; set; }
        public DateTime? FechaUltimaUbicacion { get; set; }

        // Punto de destino fijo (marcado al crear la orden)
        public decimal? DestinoLat { get; set; }
        public decimal? DestinoLng { get; set; }
    }
}
