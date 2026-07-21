namespace JLTecnico.Auth.DTOs
{
    public class ProductoItem
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? CategoriaId { get; set; }
        public string? NombreCategoria { get; set; }
        public string UnidadMedida { get; set; } = "Unidad";
        public decimal PrecioUnitario { get; set; }
        public decimal CostoUnitario { get; set; }
        public int Stock { get; set; }
        public bool Activo { get; set; }

    }
    }