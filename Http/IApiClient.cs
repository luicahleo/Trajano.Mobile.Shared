using System.Text.Json;

namespace Trajano.Mobile.Shared.Http
{
    /// <summary>
    /// Cliente HTTP genérico hacia ICARUS.API. Combina la construcción de HttpClient vía
    /// IHttpClientFactory (patrón correcto que ya usaba IMGA) con la capa de wrapper de
    /// servicio (Get/Post/Put con manejo de errores centralizado) que ya tenía IMCA — antes
    /// IMGA no tenía esta capa (cada servicio reimplementaba serialización/headers/try-catch
    /// a mano) e IMCA construía su HttpClient manualmente en vez de vía factory.
    /// </summary>
    public interface IApiClient
    {
        Task<TResponse?> GetAsync<TResponse>(string endpoint);
        Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data);

        /// <summary>
        /// Variantes que devuelven el HttpResponseMessage crudo, para llamadores que necesitan
        /// inspeccionar el status code o el cuerpo de error manualmente en vez de deserializar
        /// una respuesta tipada (ej. mostrar el mensaje de error real del servidor al usuario).
        /// </summary>
        Task<HttpResponseMessage> GetRawAsync(string endpoint, CancellationToken cancellationToken = default);
        Task<HttpResponseMessage> PostRawAsync<TRequest>(string endpoint, TRequest? data);
        Task<HttpResponseMessage> PutRawAsync<TRequest>(string endpoint, TRequest data);
        Task<HttpResponseMessage> DeleteRawAsync(string endpoint);

        /// <summary>
        /// Descarga un recurso binario (ej. una foto) desde una URL absoluta o relativa a la
        /// base configurada. Reemplaza los HttpClient ad-hoc que IMCA construía manualmente
        /// en TrabajadorService/ConfiguracionViewModel solo para este propósito.
        /// </summary>
        Task<byte[]?> DownloadBytesAsync(string urlAbsolutaORelativa);

        /// <summary>
        /// Configura el token JWT para peticiones autenticadas (header Authorization: Bearer).
        /// </summary>
        void SetAuthToken(string token);

        /// <summary>
        /// Limpia el token JWT configurado.
        /// </summary>
        void ClearAuthToken();

        /// <summary>
        /// URL base (scheme://host:puerto, sin el path /api/) para construir URLs de recursos
        /// que no viven bajo /api/ (ej. fotos servidas desde wwwroot).
        /// </summary>
        string? GetBaseUrl();

        /// <summary>
        /// Opciones de serialización usadas internamente (PropertyNameCaseInsensitive=true,
        /// necesario porque ICARUS.API devuelve camelCase por defecto). Expuestas para que los
        /// llamadores de los métodos *RawAsync deserialicen manualmente con la misma
        /// configuración, en vez de asumir (incorrectamente) las opciones por defecto de
        /// System.Text.Json.
        /// </summary>
        JsonSerializerOptions Options { get; }
    }
}
