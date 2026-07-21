namespace JLTecnico.Auth.DTOs
{
    public class AuditLogPaginadoResponse
    {
        public List<AuditLogItem> Items { get; set; } = new();
        public int TotalRegistros { get; set; }
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; }
    }
}
