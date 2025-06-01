using System.Text.Json.Serialization;

namespace FrontEdu.Models.Auth
{
    public class AuthResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}
