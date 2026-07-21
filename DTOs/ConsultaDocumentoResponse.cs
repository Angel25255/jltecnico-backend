namespace JLTecnico.Auth.DTOs
{
    public class ConsultaDocumentoResponse
    {
        public bool Encontrado { get; set; }
        public string? NombreORazonSocial { get; set; }
        public string? Direccion { get; set; } // solo suele venir para RUC
        public string? Mensaje { get; set; }
    }
}
