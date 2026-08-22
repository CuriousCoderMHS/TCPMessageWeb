using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

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

            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync("/api/test/astm/connect", request);

            response.EnsureSuccessStatusCode();
        }

        public async Task<bool> GetAstmStatusAsync()
        {
            HttpResponseMessage response =
                await _httpClient.GetAsync("/api/test/astm/status");

            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<StatusResponse>();

            return result?.Connected ?? false;
        }

        public async Task DisconnectAstmAsync()
        {
            HttpResponseMessage response =
                await _httpClient.PostAsync("/api/test/astm/disconnect", null);

            response.EnsureSuccessStatusCode();
        }

        private class StatusResponse
        {
            public bool Connected { get; set; }
        }

        public async Task SendAstmAsync (string message)
        {
            var request = new
            {
                Message = message
            };

            const string endpoint = "/api/test/astm/send";

            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(endpoint, request);

            if(!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Error sending ASTM message: {response.StatusCode}\r\n + Response: {body}");
            }

            response.EnsureSuccessStatusCode();
        }

        public async Task StartAstmHostAsync(int  port)
        {
            string endpoint = $"/api/test/astm/host/start?port={port}";

            HttpResponseMessage response = await _httpClient.PostAsync(endpoint, null);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();

                throw new Exception($"Host start failed: " + $"{response.StatusCode}\r\n{body}");
            }
        }

        public async Task StopAstmHostAsyn()
        {
            const string endpoint = "/api/test/astm/host/stop";

            HttpResponseMessage response = await _httpClient.PostAsync(endpoint,null);

            if(!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                throw new Exception(
                    $"Host stop failed: " + $"{response.StatusCode}\r\n{body}");
            }
        }
    }
}