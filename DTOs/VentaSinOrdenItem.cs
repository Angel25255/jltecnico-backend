namespace JLTecnico.Auth.DTOs
{
    public class VentaSinOrdenItem
    {
        public int VentaId { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime FechaVenta { get; set; }
    }
}
