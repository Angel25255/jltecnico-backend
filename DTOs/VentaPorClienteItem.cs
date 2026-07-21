namespace JLTecnico.Auth.DTOs
{
    public class VentaPorClienteItem
    {
        public string NombreCliente { get; set; } = string.Empty;
        public string NumeroDocumento { get; set; } = string.Empty;
        public int CantidadCompras { get; set; }
        public decimal TotalComprado { get; set; }
    }
}
