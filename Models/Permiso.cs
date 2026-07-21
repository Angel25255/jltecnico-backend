namespace JLTecnico.Auth.Models
{

    public class Permiso
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;       // ej. VENTAS_VER
        public string Modulo { get; set; } = string.Empty;       // ej. Ventas
        public string Descripcion { get; set; } = string.Empty;
    }
}
