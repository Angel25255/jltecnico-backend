namespace JLTecnico.Auth.DTOs
{
    public class VentaPorProductoItem
    {
        public string NombreProducto { get; set; } = string.Empty;
        public string? Codigo { get; set; }
        public int CantidadVendida { get; set; }
        public decimal TotalVendido { get; set; }
        public decimal CostoTotal { get; set; }
        public decimal Ganancia { get; set; }
    }
}
