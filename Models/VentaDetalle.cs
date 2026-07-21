namespace JLTecnico.Auth.Models
{
    public class VentaDetalle
    {
        public int Id { get; set; }
        public int VentaId { get; set; }
        public Venta? Venta { get; set; }
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal CostoUnitario { get; set; } // costo "congelado" al momento de la venta
        public decimal Subtotal { get; set; }
        public bool AgregadoEnCampo { get; set; } = false; // true = se agregó durante una Orden de Servicio (no en el mostrador)
    }
}
