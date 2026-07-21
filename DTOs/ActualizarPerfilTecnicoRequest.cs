namespace JLTecnico.Auth.DTOs
{
    public class ActualizarPerfilTecnicoRequest
    {
        public string? Especialidad { get; set; }
        public string EstadoDisponibilidad { get; set; } = "Disponible";
        public decimal? CalificacionPromedio { get; set; }
        public int TotalServiciosCompletados { get; set; }
    }
}
