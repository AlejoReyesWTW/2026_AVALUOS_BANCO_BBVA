using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using BotBBVA.Contratos;

namespace BotBBVA.Panel.Servicios;

/// <summary>
/// Obtiene el token OAuth2 de DocLM (client credentials) contra Microsoft Entra ID.
/// </summary>
public sealed class DocLmAutenticador
{
    private readonly ConfigOperativa _configuracion;
    private readonly HttpClient _http;

    public DocLmAutenticador(ConfigOperativa configuracion)
    {
        _configuracion = configuracion;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Solicita un token real a Entra ID usando las credenciales configuradas.
    /// </summary>
    public async Task<ResultadoConexion> ProbarConexionAsync()
    {
        var plantilla = _configuracion.Endpoints.First(e => e.Nombre == "Token de autenticación").Url;
        var url = plantilla.Replace("{TenantId}", _configuracion.TenantId);

        if (string.IsNullOrWhiteSpace(_configuracion.TenantId)
            || string.IsNullOrWhiteSpace(_configuracion.ClientId)
            || string.IsNullOrWhiteSpace(_configuracion.ClientSecret))
        {
            return new ResultadoConexion(false, "Faltan credenciales de DocLM. Completá tenant_id, client_id y client_secret en Configuración.");
        }

        var cuerpo = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _configuracion.ClientId,
            ["client_secret"] = _configuracion.ClientSecret,
            ["scope"] = _configuracion.Scope,
            ["grant_type"] = "client_credentials"
        });

        try
        {
            var respuesta = await _http.PostAsync(url, cuerpo);
            var texto = await respuesta.Content.ReadAsStringAsync();

            if (respuesta.IsSuccessStatusCode)
            {
                var token = JsonSerializer.Deserialize<RespuestaToken>(texto);
                return new ResultadoConexion(true, $"Token obtenido (vence en {token?.ExpiresIn ?? 0} s).");
            }

            var error = JsonSerializer.Deserialize<RespuestaError>(texto);
            return new ResultadoConexion(false, $"{error?.Error}: {error?.ErrorDescription}");
        }
        catch (Exception ex)
        {
            return new ResultadoConexion(false, $"No se pudo conectar a Entra ID: {ex.Message}");
        }
    }

    private sealed class RespuestaToken
    {
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class RespuestaError
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }
}

/// <summary>
/// Resultado de una prueba de conexión a un sistema externo.
/// </summary>
public sealed record ResultadoConexion(bool Exito, string Mensaje);
