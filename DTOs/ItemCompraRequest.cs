namespace JLTecnico.Auth.DTOs
{
    public class ItemCompraRequest
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal CostoUnitario { get; set; }
    }
}
