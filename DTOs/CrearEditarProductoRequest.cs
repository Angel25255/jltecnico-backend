namespace JLTecnico.Auth.DTOs
{
    public class CrearEditarProductoRequest
    {
        public string? Codigo { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? CategoriaId { get; set; }
        public string UnidadMedida { get; set; } = "Unidad";
        public decimal PrecioUnitario { get; set; }
        public decimal CostoUnitario { get; set; }
        public int Stock { get; set; }
    }
    
    }
