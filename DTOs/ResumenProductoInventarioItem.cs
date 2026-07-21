namespace JLTecnico.Auth.DTOs
{
    public class ResumenProductoInventarioItem
    {

        public int ProductoId { get; set; }
        public string? Codigo { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? NombreCategoria { get; set; }
        public int StockActual { get; set; }

        public decimal CostoUnitarioActual { get; set; }  // lo que cuesta comprarlo hoy
        public decimal PrecioVentaActual { get; set; }     // a cuánto se vende hoy (incl. IGV)

        public int CantidadComprada { get; set; }
        public decimal TotalComprado { get; set; }

        public int CantidadVendida { get; set; }
        public decimal TotalVendido { get; set; }

        public decimal GananciaTotal { get; set; }
    }
}
