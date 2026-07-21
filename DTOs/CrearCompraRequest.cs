namespace JLTecnico.Auth.DTOs
{
    public class CrearCompraRequest
    {
        public int ProveedorId { get; set; }
        public string? NumeroDocumento { get; set; }
        public List<ItemCompraRequest> Items { get; set; } = new();
    }
}
