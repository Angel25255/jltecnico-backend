namespace JLTecnico.Auth.DTOs
{
    public class ResumenDashboardResponse
    {
        public decimal VentasHoyTotal { get; set; }
        public int VentasHoyCantidad { get; set; }

        public decimal VentasMesTotal { get; set; }
        public int VentasMesCantidad { get; set; }
        public decimal GananciaMes { get; set; }

        public int CotizacionesPendientes { get; set; }
        public decimal MontoCotizadoPendiente { get; set; }

        public OrdenesActivasPorEstado OrdenesActivas { get; set; } = new();

        public int TecnicosDisponibles { get; set; }
        public int TecnicosTotal { get; set; }

        public int ProductosStockBajo { get; set; }

        public List<VentaDiaItem> VentasUltimos7Dias { get; set; } = new();
        public List<TopProductoDashboardItem> TopProductosMes { get; set; } = new();
    }
}
