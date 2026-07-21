namespace JLTecnico.Auth.DTOs
{
    public class VentaPorPeriodoItem
    {
        public string Periodo { get; set; } = string.Empty; // "2026-07-15" o "2026-07"
        public decimal Total { get; set; }
        public int CantidadVentas { get; set; }
    }
}
