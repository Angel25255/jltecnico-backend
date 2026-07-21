namespace JLTecnico.Auth.Models
{
    public class PerfilTecnico
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }
        public string? Especialidad { get; set; }
        public string EstadoDisponibilidad { get; set; } = "Disponible"; // Disponible, Ocupado, Ausente
        public decimal? CalificacionPromedio { get; set; } // 0.00 a 5.00
        public int TotalServiciosCompletados { get; set; } = 0;
    }
}
