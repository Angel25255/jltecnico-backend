namespace JLTecnico.Auth.DTOs
{
    public class CrearClienteRequest
    {
        public string TipoDocumento { get; set; } = "DNI";
        public string NumeroDocumento { get; set; } = string.Empty;
        public string NombreORazonSocial { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
    }
}
