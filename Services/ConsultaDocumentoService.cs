using System.Net.Http.Headers;
using System.Text.Json;
using JLTecnico.Auth.DTOs;

namespace JLTecnico.Auth.Services;

// Consulta datos de DNI/RUC contra Decolecta (https://decolecta.com) usando
// el token configurado. El token NUNCA se expone al frontend: todas las
// llamadas pasan por aquí, del lado del servidor.
//
// MODO DIAGNÓSTICO TEMPORAL: mientras confirmamos que la integración
// funciona, el mensaje de error incluye el código HTTP y la respuesta
// cruda del servicio externo, para poder ver el problema real sin
// tener que buscar en la consola. Una vez funcione, se puede limpiar
// el mensaje para el usuario final.
//
// NOTA: los nombres de propiedad del JSON de Decolecta (nombres, apellidos,
// razonSocial, etc.) están puestos con la mejor estimación posible, pero
// no están verificados contra la documentación real de Decolecta. Si el
// nombre no aparece, el modo diagnóstico te va a mostrar el contenido
// crudo de la respuesta en el mensaje - con eso ajustamos los nombres
// de propiedad en un minuto.
public class ConsultaDocumentoService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ConsultaDocumentoService> _logger;

    public ConsultaDocumentoService(HttpClient http, IConfiguration config, ILogger<ConsultaDocumentoService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;

        var token = _config["Decolecta:ApiKey"];
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _http.BaseAddress = new Uri("https://api.decolecta.com/");
    }

    public async Task<ConsultaDocumentoResponse> ConsultarDni(string dni)
    {
        try
        {
            var respuesta = await _http.GetAsync($"v1/reniec/dni?numero={dni}");
            string contenido = await respuesta.Content.ReadAsStringAsync();

            _logger.LogInformation("Respuesta cruda Decolecta (DNI {Dni}) - Codigo {Codigo}: {Contenido}",
                dni, (int)respuesta.StatusCode, contenido);

            if (!respuesta.IsSuccessStatusCode)
            {
                return new ConsultaDocumentoResponse
                {
                    Encontrado = false,
                    Mensaje = $"[DIAGNÓSTICO] Decolecta respondió código {(int)respuesta.StatusCode}: {contenido}"
                };
            }

            using var doc = JsonDocument.Parse(contenido);
            var raiz = doc.RootElement;

            // Decolecta suele devolver un campo con el nombre completo ya armado
            // (ej. "full_name"); si no viene, intentamos armarlo con nombres/apellidos sueltos.
            string nombreCompleto = raiz.TryGetProperty("full_name", out var fn) ? fn.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(nombreCompleto))
            {
                string nombres = raiz.TryGetProperty("first_name", out var n) ? n.GetString() ?? "" : "";
                string apPaterno = raiz.TryGetProperty("first_last_name", out var ap) ? ap.GetString() ?? "" : "";
                string apMaterno = raiz.TryGetProperty("second_last_name", out var am) ? am.GetString() ?? "" : "";
                nombreCompleto = $"{nombres} {apPaterno} {apMaterno}".Trim();
            }

            if (string.IsNullOrWhiteSpace(nombreCompleto))
            {
                return new ConsultaDocumentoResponse
                {
                    Encontrado = false,
                    Mensaje = $"[DIAGNÓSTICO] Respuesta 200 pero sin nombre reconocible. Contenido: {contenido}"
                };
            }

            return new ConsultaDocumentoResponse { Encontrado = true, NombreORazonSocial = nombreCompleto };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando DNI {Dni} en Decolecta", dni);
            return new ConsultaDocumentoResponse
            {
                Encontrado = false,
                Mensaje = $"[DIAGNÓSTICO] Excepción: {ex.Message}"
            };
        }
    }

    public async Task<ConsultaDocumentoResponse> ConsultarRuc(string ruc)
    {
        try
        {
            var respuesta = await _http.GetAsync($"v1/sunat/ruc?numero={ruc}");
            string contenido = await respuesta.Content.ReadAsStringAsync();

            _logger.LogInformation("Respuesta cruda Decolecta (RUC {Ruc}) - Codigo {Codigo}: {Contenido}",
                ruc, (int)respuesta.StatusCode, contenido);

            if (!respuesta.IsSuccessStatusCode)
            {
                return new ConsultaDocumentoResponse
                {
                    Encontrado = false,
                    Mensaje = $"[DIAGNÓSTICO] Decolecta respondió código {(int)respuesta.StatusCode}: {contenido}"
                };
            }

            using var doc = JsonDocument.Parse(contenido);
            var raiz = doc.RootElement;

            string razonSocial = raiz.TryGetProperty("razon_social", out var rs) ? rs.GetString() ?? "" : "";
            string direccion = raiz.TryGetProperty("direccion", out var d) ? d.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(razonSocial))
            {
                return new ConsultaDocumentoResponse
                {
                    Encontrado = false,
                    Mensaje = $"[DIAGNÓSTICO] Respuesta 200 pero sin razón social reconocible. Contenido: {contenido}"
                };
            }

            return new ConsultaDocumentoResponse { Encontrado = true, NombreORazonSocial = razonSocial, Direccion = direccion };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando RUC {Ruc} en Decolecta", ruc);
            return new ConsultaDocumentoResponse
            {
                Encontrado = false,
                Mensaje = $"[DIAGNÓSTICO] Excepción: {ex.Message}"
            };
        }
    }
}