namespace JLTecnico.Auth.Models
{
    public class Compra
    {
        public int Id { get; set; }
        public int ProveedorId { get; set; }
        public Proveedor? Proveedor { get; set; }
        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }
        public DateTime FechaCompra { get; set; } = DateTime.UtcNow;
        public decimal Total { get; set; }
        public string? NumeroDocumento { get; set; }

        public List<CompraDetalle> Detalles { get; set; } = new();
    }
}
