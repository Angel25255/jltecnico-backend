namespace JLTecnico.Auth.DTOs
{
    public class PerfilTecnicoItem
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public bool Activo { get; set; } // el usuario (login) está activo
        public string? Especialidad { get; set; }
        public string EstadoDisponibilidad { get; set; } = "Disponible";
        public decimal? CalificacionPromedio { get; set; }
        public int TotalServiciosCompletados { get; set; }
        public int OrdenesActivas { get; set; } // 0 hasta que exista el módulo de Órdenes de Servicio
    }
}
