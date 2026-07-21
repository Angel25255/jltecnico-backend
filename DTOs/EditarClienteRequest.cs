namespace JLTecnico.Auth.DTOs
{
    public class EditarClienteRequest
    {
        public string NombreORazonSocial { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
    }
}
