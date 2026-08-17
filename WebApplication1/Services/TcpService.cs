using System.Net.Sockets;

namespace WebApplication1.Services
{
    public class TcpService
    {
        private TcpClient? _client;
        public async Task ConnectAsync(string ipAdress, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(ipAdress, port);
        }

        public bool IsConnected()
        {
            return _client?.Connected ?? false;
        }

        public void Disconnect()
        {
            _client?.Close();
            _client = null;
        }

        public async Task SendAsync(string message)
        {
            if (_client == null || !_client.Connected)
            {
                throw new InvalidOperationException("Not connected to a TCP server.");
            }

            NetworkStream stream = _client.GetStream();
            byte[] data = System.Text.Encoding.UTF8.GetBytes(message + "\r\n");
            await stream.WriteAsync(data);
        }

    }
}
