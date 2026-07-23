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

            bool valido = raiz.TryGetProperty("valid", out var elementoValido) && elementoValido.GetBoolean();
            string? operador = raiz.TryGetProperty("carrier", out var elementoCarrier) ? elementoCarrier.GetString() : null;
            string? tipo = raiz.TryGetProperty("type", out var elementoTipo) ? elementoTipo.GetString() : null;

            return new TelefonoValidoResultado
            {
                Valido = valido,
                Operador = operador,
                Tipo = tipo,
                Mensaje = valido
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