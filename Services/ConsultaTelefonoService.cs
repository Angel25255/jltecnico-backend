using System.Text.Json;

namespace JLTecnico.Auth.Services;

// Consulta si un número de celular es real usando AbstractAPI
// (Phone Validation). No confirma que la persona tenga el celular
// en la mano ahora mismo (para eso haría falta mandar un SMS), pero
// sí confirma que el número existe de verdad en la red del operador
// y no es simplemente una secuencia inventada.
public class ConsultaTelefonoService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuracion;

    public ConsultaTelefonoService(HttpClient httpClient, IConfiguration configuracion)
    {
        _httpClient = httpClient;
        _configuracion = configuracion;
    }

    public async Task<TelefonoValidoResultado> Validar(string numero)
    {
        string apiKey = _configuracion["AbstractApi:PhoneValidationKey"] ?? "";

        // Los celulares peruanos tienen 9 dígitos - si no viene con
        // código de país, se lo agregamos (+51) para la consulta.
        string numeroLimpio = numero.Trim();
        string numeroFormateado = numeroLimpio.StartsWith("+") ? numeroLimpio : $"+51{numeroLimpio}";

        try
        {
            string url = $"https://phoneintelligence.abstractapi.com/v1?api_key={apiKey}&phone={Uri.EscapeDataString(numeroFormateado)}";
            var respuesta = await _httpClient.GetAsync(url);

            if (!respuesta.IsSuccessStatusCode)
            {
                return new TelefonoValidoResultado
                {
                    Valido = false,
                    Mensaje = "No se pudo conectar al servicio de validación en este momento."
                };
            }

            var contenido = await respuesta.Content.ReadAsStringAsync();
            using var documento = JsonDocument.Parse(contenido);
            var raiz = documento.RootElement;

            // La respuesta real de AbstractAPI anida los datos así:
            // { "phone_validation": { "is_valid": true, "line_status": "active" },
            //   "phone_carrier": { "name": "Claro", "line_type": "mobile" }, ... }
            bool valido = false;
            string? lineStatus = null;
            if (raiz.TryGetProperty("phone_validation", out var validacion))
            {
                if (validacion.TryGetProperty("is_valid", out var elementoValido) && elementoValido.ValueKind == JsonValueKind.True)
                    valido = true;
                if (validacion.TryGetProperty("line_status", out var elementoEstadoLinea))
                    lineStatus = elementoEstadoLinea.GetString();
            }

            string? operador = null;
            string? tipo = null;
            if (raiz.TryGetProperty("phone_carrier", out var operadorInfo))
            {
                if (operadorInfo.TryGetProperty("name", out var elementoNombre))
                    operador = elementoNombre.GetString();
                if (operadorInfo.TryGetProperty("line_type", out var elementoTipoLinea))
                    tipo = elementoTipoLinea.GetString();
            }

            // Se considera realmente válido si is_valid=true Y la línea está activa
            // (si el operador no informa el estado, nos quedamos con is_valid solo)
            bool valeLaPena = valido && (lineStatus == null || lineStatus == "active");

            return new TelefonoValidoResultado
            {
                Valido = valeLaPena,
                // OJO: el nombre del operador (Claro/Movistar/Bitel/Entel) que
                // devuelve esta API NO es confiable para Perú - por la
                // portabilidad numérica, suele adivinar mal. Por eso ya NO
                // se usa para decidir nada, solo queda el campo "Tipo"
                // (móvil/fijo), que sí es más estable.
                Operador = null,
                Tipo = tipo,
                Mensaje = valeLaPena
                    ? "Este número existe y está activo."
                    : "Este número no parece ser real (no se encontró en ningún operador)."
            };
        }
        catch
        {
            return new TelefonoValidoResultado
            {
                Valido = false,
                Mensaje = "No se pudo conectar al servicio de validación en este momento."
            };
        }
    }
}

public class TelefonoValidoResultado
{
    public bool Valido { get; set; }
    public string? Operador { get; set; }
    public string? Tipo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}