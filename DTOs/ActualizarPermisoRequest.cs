namespace JLTecnico.Auth.DTOs
{
    public class ActualizarPermisoRequest
    {
        public int PermisoId { get; set; }
        public string Rol { get; set; } = string.Empty; // "Vendedor" o "Tecnico"
        public bool Permitido { get; set; }
    }
}
