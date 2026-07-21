namespace JLTecnico.Auth.Models
{
    public class RolPermiso
    {
        public int Id { get; set; }
        public string Rol { get; set; } = string.Empty; // Vendedor, Tecnico (Administrador siempre tiene todo)
        public int PermisoId { get; set; }
        public Permiso? Permiso { get; set; }
        public bool Permitido { get; set; }
    }
}

