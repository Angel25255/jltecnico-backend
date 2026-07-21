namespace JLTecnico.Auth.DTOs
{
    public class ResumenGeneralResponse
    {
        public decimal TotalVendido { get; set; }
        public int CantidadVentas { get; set; }
        public decimal TicketPromedio { get; set; }
        public int CotizacionesPendientes { get; set; }
        public int CotizacionesAprobadas { get; set; }
        public decimal MontoCotizadoPendiente { get; set; }
        public int ProductosStockBajo { get; set; }
        public int VentasAnuladas { get; set; }
        public decimal GananciaTotal { get; set; }       // Total vendido - costo de lo vendido
        public decimal TotalComprado { get; set; }        // Cuánto se gastó en compras en el periodo
    }
}