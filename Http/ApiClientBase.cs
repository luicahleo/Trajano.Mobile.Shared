using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Trajano.Mobile.Shared.Logging;

namespace Trajano.Mobile.Shared.Http
{
    /// <inheritdoc cref="IApiClient"/>
    public class ApiClientBase : IApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ISharedLogger _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonSerializerOptions Options => _jsonOptions;

        public ApiClientBase(HttpClient httpClient, ISharedLogger logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<TResponse?> GetAsync<TResponse>(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                await _logger.LogErrorAsync("ApiClientBase.GetAsync - Endpoint vacío", "ApiClient");
                return default;
            }

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    await _logger.LogErrorAsync($"ApiClientBase.GetAsync - Error HTTP {response.StatusCode}: {endpoint}", "ApiClient");
                    return default;
                }

                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                await _logger.LogErrorAsync($"ApiClientBase.GetAsync - Error de conexión: {ex.Message} | {endpoint}", "ApiClient");
                return default;
            }
            catch (TaskCanceledException ex)
            {
                await _logger.LogErrorAsync($"ApiClientBase.GetAsync - Timeout: {ex.Message} | {endpoint}", "ApiClient");
                return default;
            }
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                await _logger.LogErrorAsync("ApiClientBase.PostAsync - Endpoint vacío", "ApiClient");
                return default;
            }

            if (data == null)
            {
                await _logger.LogErrorAsync("ApiClientBase.PostAsync - Data es nulo", "ApiClient");
                return default;
            }

            StringContent content = BuildJsonContent(data);

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    await _logger.LogErrorAsync($"ApiClientBase.PostAsync - Error HTTP {response.StatusCode}: {endpoint} | {errorBody}", "ApiClient");
                    return default;
                }

                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                await _logger.LogErrorAsync($"ApiClientBase.PostAsync - Error de conexión: {ex.Message} | {endpoint}", "ApiClient");
                return default;
            }
            catch (TaskCanceledException ex)
            {
                await _logger.LogErrorAsync($"ApiClientBase.PostAsync - Timeout: {ex.Message} | {endpoint}", "ApiClient");
                return default;
            }
        }

        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                await _logger.LogErrorAsync("ApiClientBase.PutAsync - Endpoint vacío", "ApiClient");
                return default;
            }

            if (data == null)
            {
                await _logger.LogErrorAsync("ApiClientBase.PutAsync - Data es nulo", "ApiClient");
                return default;
            }

            StringContent content = BuildJsonContent(data);

            try
            {
                HttpResponseMessage response = await _httpClient.PutAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    await _logger.LogErrorAsync($"ApiClientBase.PutAsync - Error HTTP {response.StatusCode}: {endpoint} | {errorBody}", "ApiClient");
                    return default;
                }

                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                await _logger.LogErrorAsync($"ApiClientBase.PutAsync - Error de conexión: {ex.Message} | {endpoint}", "ApiClient");
                return default;
            }
            catch (TaskCanceledException ex)
            {
                await _logger.LogErrorAsync($"ApiClientBase.PutAsync - Timeout: {ex.Message} | {endpoint}", "ApiClient");
                return default;
            }
        }

        public async Task<byte[]?> DownloadBytesAsync(string urlAbsolutaORelativa)
        {
            if (string.IsNullOrWhiteSpace(urlAbsolutaORelativa))
            {
                await _logger.LogErrorAsync("ApiClientBase.DownloadBytesAsync - URL vacía", "ApiClient");
                return null;
            }

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(urlAbsolutaORelativa);

                if (!response.IsSuccessStatusCode)
                {
                    await _logger.LogErrorAsync($"ApiClientBase.DownloadBytesAsync - Error HTTP {response.StatusCode}: {urlAbsolutaORelativa}", "ApiClient");
                    return null;
                }

                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (HttpRequestException ex)
            {
                await _logger.LogErrorAsync($"ApiClientBase.DownloadBytesAsync - Error de conexión: {ex.Message} | {urlAbsolutaORelativa}", "ApiClient");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                await _logger.LogErrorAsync($"ApiClientBase.DownloadBytesAsync - Timeout: {ex.Message} | {urlAbsolutaORelativa}", "ApiClient");
                return null;
            }
        }

        public void SetAuthToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public void ClearAuthToken()
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<HttpResponseMessage> GetRawAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            return await _httpClient.GetAsync(endpoint, cancellationToken);
        }

        public async Task<HttpResponseMessage> PostRawAsync<TRequest>(string endpoint, TRequest? data)
        {
            HttpContent? content = data == null ? null : BuildJsonContent(data);
            return await _httpClient.PostAsync(endpoint, content);
        }

        public async Task<HttpResponseMessage> PutRawAsync<TRequest>(string endpoint, TRequest data)
        {
            return await _httpClient.PutAsync(endpoint, BuildJsonContent(data));
        }

        public async Task<HttpResponseMessage> DeleteRawAsync(string endpoint)
        {
            return await _httpClient.DeleteAsync(endpoint);
        }

        private StringContent BuildJsonContent<TRequest>(TRequest data)
        {
            string json = JsonSerializer.Serialize(data, _jsonOptions);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        public string? GetBaseUrl()
        {
            Uri? baseAddress = _httpClient.BaseAddress;
            if (baseAddress == null)
            {
                return null;
            }

            string baseUrl = $"{baseAddress.Scheme}://{baseAddress.Host}";
            if (!baseAddress.IsDefaultPort)
            {
                baseUrl += $":{baseAddress.Port}";
            }

            return baseUrl;
        }
    }
}
