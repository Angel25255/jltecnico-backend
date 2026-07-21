namespace JLTecnico.Auth.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? CategoriaId { get; set; }
        public Categoria? Categoria { get; set; }
        public string UnidadMedida { get; set; } = "Unidad";
        public decimal PrecioUnitario { get; set; } // precio de venta, incluye IGV
        public decimal CostoUnitario { get; set; } // costo de compra vigente (se actualiza con cada compra)
        public int Stock { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
