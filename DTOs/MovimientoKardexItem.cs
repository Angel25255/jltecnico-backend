namespace JLTecnico.Auth.DTOs
{
    public class MovimientoKardexItem
    {
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = string.Empty; // "Entrada (Compra)" o "Salida (Venta)"
        public int Cantidad { get; set; } // positivo = entrada, negativo = salida
        public string Referencia { get; set; } = string.Empty; // "Compra #12" o "Venta #45"
        public string? NombreTercero { get; set; } // proveedor o cliente según corresponda
    }
}
