namespace JLTecnico.Auth.Models
{
    public class OrdenServicioProducto
    {
        public int Id { get; set; }
        public int OrdenServicioId { get; set; }
        public OrdenServicio? OrdenServicio { get; set; }
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal CostoUnitario { get; set; }
    }
}
