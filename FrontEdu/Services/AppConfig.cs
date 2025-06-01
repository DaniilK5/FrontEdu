using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Diagnostics;

namespace FrontEdu.Services
{
    public static class AppConfig
    {
        public static string ApiBaseUrl
        {
            get
            {
#if DEBUG
                if (DeviceInfo.Platform == DevicePlatform.Android)
                    return "http://10.0.2.2:5105"; // Специальный IP для Android эмулятора
                return "http://localhost:5105";
#else
                return "https://your-production-api-url/";
#endif
            }
            set { }
        }
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
            try
            {
                if (_httpClient == null)
                {
                    var handler = new HttpClientHandler();
#if DEBUG
                    handler.ServerCertificateCustomValidationCallback = 
                        (message, cert, chain, sslPolicyErrors) => true;
#endif
                    _httpClient = new HttpClient(handler)
                    {
                        BaseAddress = new Uri(ApiBaseUrl),
                        Timeout = TimeSpan.FromSeconds(30)
                    };
                }

                // Всегда обновляем токен при создании или получении клиента
                var token = await SecureStorage.GetAsync(AUTH_TOKEN_KEY);
                Debug.WriteLine($"Current token: {token?.Substring(0, Math.Min(token?.Length ?? 0, 20))}...");

                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                else
                {
                    Debug.WriteLine("No token found in SecureStorage");
                    _httpClient.DefaultRequestHeaders.Authorization = null;
                }

                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                return _httpClient;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating HttpClient: {ex}");
                throw;
            }
        }

        public static void ResetHttpClient()
        {
            _httpClient?.Dispose();
            _httpClient = null;
        }
    }
}
