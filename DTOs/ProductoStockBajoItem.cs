namespace JLTecnico.Auth.DTOs
{
    public class ProductoStockBajoItem
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Codigo { get; set; }
        public string? NombreCategoria { get; set; }
        public int Stock { get; set; }
    }
}
