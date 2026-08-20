using System.Net.Sockets;
using System.Text;

namespace TCPMessageAPI.Services
{
    public class TcpService
    {
        private TcpClient? _client;
        private readonly StringBuilder _receiveBuffer = new();
        public async Task ConnectAsync(string ipAdress, int port)
        {
            if (_client != null && _client.Connected)
            {
                throw new InvalidOperationException("Already connected to a TCP server.");
            }
            _client = new TcpClient();
            await _client.ConnectAsync(ipAdress, port);
        }

        public bool IsConnected()
        {
            return _client?.Connected ?? false;
        }

        public async Task SendAsync(string message)
        {
            if (_client == null || !_client.Connected)
            {
                throw new InvalidOperationException("Not connected to a TCP server.");
            }

            NetworkStream stream = _client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(message + "\r\n");
            await stream.WriteAsync(data);
        }

        public async Task<string> ReceiveAsync()
        {
            if (_client == null || !_client.Connected)
            {
                throw new InvalidOperationException("Not connected to a TCP server.");
            }

            NetworkStream stream = _client.GetStream();
            byte[] buffer = new byte[1024];

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Connection closed by the server.");
                }

                string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                _receiveBuffer.Append(received);

                string currentData = _receiveBuffer.ToString();

                int newlineIndex = currentData.IndexOf('\n');

                if (newlineIndex >= 0)
                {
                    string message = currentData.Substring(0, newlineIndex).TrimEnd('\r');

                    _receiveBuffer.Remove(0, newlineIndex + 1);

                    return message;
                }
            }
        }

        public void Disconnect()
        {
            _client?.Close();
            _client = null;

            _receiveBuffer.Clear();
        }

    }
}