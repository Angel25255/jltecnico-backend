namespace JLTecnico.Auth.Models;

public class Sesion
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string TokenId { get; set; } = string.Empty; // jti del JWT
    public string IP { get; set; } = string.Empty;
    public string? UserAgent { get; set; }

    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime FechaUltimoUso { get; set; } = DateTime.UtcNow;
    public bool Activa { get; set; } = true;
}
