using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace FrontEdu.Services
{
    public static class AppConfig
    {
        public static string ApiBaseUrl { get; set; } = "http://localhost:5105";
        private static HttpClient? _httpClient;
        private const string AUTH_TOKEN_KEY = "auth_token";
        // Добавьте метод проверки токена
        public static async Task<bool> VerifyStoredToken()
        {
            try
            {
                var token = await SecureStorage.GetAsync(AUTH_TOKEN_KEY);
                return !string.IsNullOrEmpty(token);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Метод для получения токена (для отладки)
        public static async Task<string?> GetStoredToken()
        {
            return await SecureStorage.GetAsync(AUTH_TOKEN_KEY);
        }
        public static async Task<HttpClient> CreateHttpClientAsync()
        {
            if (_httpClient != null)
                return _httpClient;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(ApiBaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(30)
            };

#if DEBUG
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = 
                    (message, cert, chain, sslPolicyErrors) => true
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(ApiBaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(30)
            };
#endif

            // Получаем токен если есть
            var token = await SecureStorage.GetAsync("auth_token");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            return _httpClient;
        }

        public static void ResetHttpClient()
        {
            _httpClient?.Dispose();
            _httpClient = null;
        }
    }
}
