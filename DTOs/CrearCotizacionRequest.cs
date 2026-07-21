namespace JLTecnico.Auth.DTOs
{
    public class CrearCotizacionRequest
    {
        public int ClienteId { get; set; }
        public List<ItemCarritoRequest> Items { get; set; } = new();
    }
}
