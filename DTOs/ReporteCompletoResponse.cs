namespace JLTecnico.Auth.DTOs
{
    public class ReporteCompletoResponse
    {
        public ResumenGeneralResponse Resumen { get; set; } = new();
        public List<VentaPorPeriodoItem> VentasPorPeriodo { get; set; } = new();
        public List<VentaPorProductoItem> VentasPorProducto { get; set; } = new();
        public List<VentaPorClienteItem> VentasPorCliente { get; set; } = new();
        public List<ProductoStockBajoItem> ProductosStockBajo { get; set; } = new();
        public List<CotizacionPendienteItem> CotizacionesPendientes { get; set; } = new();
    }
}
