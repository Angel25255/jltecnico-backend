namespace JLTecnico.Auth.Models;

public class Usuario
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "Vendedor"; // Administrador, Tecnico, Vendedor
    public bool Activo { get; set; } = true;
    public string? Direccion { get; set; }

    // Doble factor (TOTP)
    public string? TotpSecret { get; set; }
    public bool TotpConfigurado { get; set; } = false;

    // Control de intentos fallidos
    public int IntentosFallidos { get; set; } = 0;
    public DateTime? BloqueadoHasta { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
