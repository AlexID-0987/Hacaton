namespace Hacaton.Services
{
    public class SilpoOAuthService
    {
        private readonly HttpClient _httpClient;

        public SilpoOAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> RegisterClientAsync()
        {
            var request = new
            {
                client_name = "Hacaton ASP.NET",

                redirect_uris = new[]
                {
                "http://localhost:5068/api/silpo/callback"
            },

                grant_types = new[]
                {
                "authorization_code"
            },

                response_types = new[]
                {
                "code"
            }
            };

            var response = await _httpClient.PostAsJsonAsync(
                "https://mcp.silpo.ua/register",
                request);

            var content = await response.Content.ReadAsStringAsync();

            return $"HTTP {(int)response.StatusCode}\n{content}";
        }
    }
}

