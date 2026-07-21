namespace JLTecnico.Auth.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        public string TipoDocumento { get; set; } = "DNI"; // "DNI" o "RUC"
        public string NumeroDocumento { get; set; } = string.Empty;
        public string NombreORazonSocial { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public int? CreadoPorUsuarioId { get; set; }
    }
}
