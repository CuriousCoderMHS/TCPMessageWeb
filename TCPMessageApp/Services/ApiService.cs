using System.Net.Http;
using System.Net.Http.Json;

namespace TCPMessageApp.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://localhost:7222")
            };
        }

        public async Task ConnectAstmAsync(string ipAddress, int port)
        {
            var request = new
            {
                IpAddress = ipAddress,
                Port = port
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/astm/connect", request);
            response.EnsureSuccessStatusCode();
        }

        public async Task <bool> GetAstmStatusAsync()
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/astm/status");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<StatusResponse>();
            return result?.Connected ?? false;
        }

        public async Task DisconnectAstmAsync()
        {
            HttpResponseMessage response = await _httpClient.PostAsync("/api/astm/disconnect", null);
            response.EnsureSuccessStatusCode();
        }

        private class StatusResponse
        {
            public bool Connected { get; set; }
        }
    }
}
