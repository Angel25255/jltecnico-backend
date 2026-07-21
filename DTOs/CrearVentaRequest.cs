namespace JLTecnico.Auth.DTOs
{
    public class CrearVentaRequest
    {
        public int ClienteId { get; set; }
        public List<ItemCarritoRequest> Items { get; set; } = new();

        // Si el cliente compra productos que requieren instalación,
        // se crea automáticamente una Orden de Servicio ligada a esta
        // misma venta.
        public bool RequiereOrdenServicio { get; set; } = false;
        public string? DireccionInstalacion { get; set; }
        public string? DescripcionServicio { get; set; }
    }
}
