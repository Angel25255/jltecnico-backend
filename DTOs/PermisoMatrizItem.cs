namespace JLTecnico.Auth.DTOs
{
    public class PermisoMatrizItem
    {
        public int PermisoId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Modulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool VendedorPermitido { get; set; }
        public bool TecnicoPermitido { get; set; }
    }
}
