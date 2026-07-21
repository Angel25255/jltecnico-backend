namespace JLTecnico.Auth.DTOs
{
    public class CompraItem
    {
        public int Id { get; set; }
        public string NombreProveedor { get; set; } = string.Empty;
        public string NombreUsuario { get; set; } = string.Empty;
        public DateTime FechaCompra { get; set; }
        public decimal Total { get; set; }
        public string? NumeroDocumento { get; set; }
        public List<CompraDetalleItem> Detalles { get; set; } = new();
    }
}
