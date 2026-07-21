using System.Net.Http.Headers;
using System.Text.Json;

namespace JLTecnico.Auth.Models
{
    public class DecolectaService
    {
            private readonly HttpClient _client;
            private readonly string _apiKey;

            public DecolectaService(IConfiguration config)
            {
                _apiKey = config["Decolecta:ApiKey"];
                _client = new HttpClient { BaseAddress = new Uri("https://api.decolecta.com/") };
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            public async Task<DniResponse?> ConsultarDniAsync(string dni)
            {
                // AJUSTAR el path exacto según la documentación de Decolecta
                var response = await _client.GetAsync($"v1/reniec/dni?numero={dni}");
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DniResponse>(json);
            }
        }

        public class DniResponse
        {
            // AJUSTAR nombres de propiedades según el JSON real que devuelva Decolecta
            public string? Nombres { get; set; }
            public string? ApellidoPaterno { get; set; }
            public string? ApellidoMaterno { get; set; }
        }
    }
