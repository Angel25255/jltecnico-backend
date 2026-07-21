namespace JLTecnico.Auth.Models;

public class DispositivoConocido
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string DispositivoHash { get; set; } = string.Empty; // hash(IP + UserAgent)
    public string IP { get; set; } = string.Empty;
    public string? UserAgent { get; set; }

    public DateTime FechaPrimerUso { get; set; } = DateTime.UtcNow;
    public DateTime FechaUltimoUso { get; set; } = DateTime.UtcNow;
}
