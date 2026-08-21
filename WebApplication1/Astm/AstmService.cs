using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using TCPMessageAPI.Hubs;

namespace TCPMessageAPI.Astm
{
    public class AstmService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        private const int TimeoutMilliseconds = 10000;
        private const int MaxRetires = 6;

        // Confirm against the target analyser's ASTM profile.
        private const int MaxFrameDataLength = 240;

        public AstmService(IHubContext<AstmHub> hubContext, ILogger<AstmService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public bool IsConnected
        {
            get
            {
                if (_client == null)
                    return false;

                try
                {
                    Socket socket = _client.Client;

                    return !(socket.Poll(1000, SelectMode.SelectRead) &&
                             socket.Available == 0);
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task ConnectAsync(string ipAddress, int port)
        {
            Disconnect();

            var client = new TcpClient();

            try
            {
                await client.ConnectAsync(ipAddress, port);

                _client = client;
                _stream = _client.GetStream();
            }
            catch
            {
                client.Dispose();
                _client = null;
                _stream = null;

                throw;
            }
        }

        public async Task<bool> EstablishCommunicationAsync()
        {
            EnsureConnected();

            await SendByteAsync(AstmConstants.ENQ);

            byte response = await ReadByteAsync();

            if (response == AstmConstants.ACK)
                return true;

            if (response == AstmConstants.NAK)
                return false;

            throw new InvalidOperationException(
                $"Unexpected ASTM response: 0x{response:X2}");
        }

        public async Task SendFrameAsync(
            string data,
            int frameNumber,
            bool lastFrame = true)
        {
            EnsureConnected();

            byte[] frame = BuildFrame(data, frameNumber, lastFrame);

            for (int attempt = 0; attempt < MaxRetires; attempt++)
            {
                await _stream!.WriteAsync(frame);

                byte response = await ReadByteAsync();

                if (response == AstmConstants.ACK)
                {
                    return;
                }

                if (response == AstmConstants.NAK)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Unexpected ASTM response after sending frame: 0x{response:X2}");
            }

            throw new InvalidOperationException(
                "ASTM failed to send frame after maximum attempts.");
        }

        public async Task EndCommunicationAsync()
        {
            EnsureConnected();
            await SendByteAsync(AstmConstants.EOT);
        }

        public void Disconnect()
        {
            _stream?.Dispose();
            _client?.Close();

            _stream = null;
            _client = null;
        }

        private byte[] BuildFrame(
            string data,
            int frameNumber,
            bool lastFrame)
        {
            if (frameNumber < 0 || frameNumber > 7)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameNumber),
                    "ASTM frame number must be between 0 and 7.");
            }

            byte terminator = lastFrame
                ? AstmConstants.ETX
                : AstmConstants.ETB;

            byte[] dataBytes = Encoding.ASCII.GetBytes(data);

            var frame = new List<byte>();

            frame.Add(AstmConstants.STX);
            frame.Add((byte)('0' + frameNumber));
            frame.AddRange(dataBytes);
            frame.Add(terminator);

            byte checksum = CalculateChecksum(frame.ToArray());

            frame.AddRange(
                Encoding.ASCII.GetBytes(checksum.ToString("X2")));

            frame.Add(AstmConstants.CR);
            frame.Add(AstmConstants.LF);

            return frame.ToArray();
        }

        private static byte CalculateChecksum(byte[] frame)
        {
            int checksum = 0;

            // Sum from frame number through ETX/ETB; STX is excluded.
            for (int i = 1; i < frame.Length; i++)
            {
                checksum += frame[i];
            }

            return (byte)(checksum & 0xFF);
        }

        private async Task SendByteAsync(byte value)
        {
            EnsureConnected();
            await _stream!.WriteAsync(new[] { value });
        }

        private async Task<byte> ReadByteAsync()
        {
            EnsureConnected();

            byte[] buffer = new byte[1];

            int bytesRead = await _stream!.ReadAsync(buffer, 0, 1);

            if (bytesRead == 0)
            {
                throw new InvalidOperationException(
                    "ASTM connection was closed.");
            }

            return buffer[0];
        }

        private void EnsureConnected()
        {
            if (!IsConnected || _stream == null)
            {
                throw new InvalidOperationException(
                    "ASTM not connected to a TCP server.");
            }
        }

        public async Task<AstmFrame> ReceiveFrameAsync()
        {
            EnsureConnected();

            byte start = await ReadByteAsync();

            while (start != AstmConstants.STX)
            {
                if (start == AstmConstants.EOT)
                {
                    throw new InvalidOperationException(
                        "ASTM connection closed by the device.");
                }

                start = await ReadByteAsync();
            }

            var frameBytes = new List<byte>
            {
                AstmConstants.STX
            };

            while (true)
            {
                byte value = await ReadByteAsync();
                frameBytes.Add(value);

                if (value == AstmConstants.ETX ||
                    value == AstmConstants.ETB)
                {
                    break;
                }
            }

            frameBytes.Add(await ReadByteAsync());
            frameBytes.Add(await ReadByteAsync());

            byte cr = await ReadByteAsync();
            byte lf = await ReadByteAsync();

            if (cr != AstmConstants.CR || lf != AstmConstants.LF)
            {
                await SendByteAsync(AstmConstants.NAK);

                throw new InvalidOperationException(
                    "ASTM frame does not end with CR LF.");
            }

            try
            {
                AstmFrame frame = AstmFrameParser.Parse(frameBytes.ToArray());

                await SendByteAsync(AstmConstants.ACK);

                return frame;
            }
            catch
            {
                await SendByteAsync(AstmConstants.NAK);
                throw;
            }
        }

        public async Task<AstmMessage> ReceiveMessageAsync()
        {
            EnsureConnected();

            var message = new AstmMessage();

            while (true)
            {
                byte control = await ReadByteAsync();

                if (control == AstmConstants.ENQ)
                {
                    await SendByteAsync(AstmConstants.ACK);
                    continue;
                }

                if (control == AstmConstants.EOT)
                {
                    return message;
                }

                if (control != AstmConstants.STX)
                {
                    continue;
                }

                var frameBytes = new List<byte>
                {
                    AstmConstants.STX
                };

                while (true)
                {
                    byte value = await ReadByteAsync();
                    frameBytes.Add(value);

                    if (value == AstmConstants.ETX ||
                        value == AstmConstants.ETB)
                    {
                        break;
                    }
                }

                // Checksum plus CR LF.
                frameBytes.Add(await ReadByteAsync());
                frameBytes.Add(await ReadByteAsync());
                frameBytes.Add(await ReadByteAsync());
                frameBytes.Add(await ReadByteAsync());

                try
                {
                    AstmFrame frame = AstmFrameParser.Parse(
                        frameBytes.ToArray());

                    message.Frames.Add(frame);

                    await SendByteAsync(AstmConstants.ACK);
                }
                catch
                {
                    await SendByteAsync(AstmConstants.NAK);
                    throw;
                }
            }
        }

        public async Task SendMessageAsync(string message)
        {
            EnsureConnected();

            List<string> frames = CreateFramePayloads(message);

            if (frames.Count == 0)
            {
                throw new ArgumentException(
                    "ASTM message must contain at least one record.",
                    nameof(message));
            }

            await SendByteAsync(AstmConstants.ENQ);

            byte response = await ReadByteAsync();

            if (response != AstmConstants.ACK)
            {
                throw new InvalidOperationException(
                    $"ASTM receiver did not ACK ENQ. Received 0x{response:X2}");
            }

            int frameNumber = 1;

            for (int index = 0; index < frames.Count; index++)
            {
                bool isLastFrame = index == frames.Count - 1;

                // Non-final frames use ETB; only the final frame uses ETX.
                await SendFrameAsync(
                    frames[index],
                    frameNumber,
                    isLastFrame);

                frameNumber = (frameNumber + 1) % 8;
            }

            await SendByteAsync(AstmConstants.EOT);
        }

        private static List<string> CreateFramePayloads(string message)
        {
            if (message.Any(c => c > 0x7F))
            {
                throw new ArgumentException(
                    "ASTM messages must contain ASCII characters only.",
                    nameof(message));
            }

            string normalized = message
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

            var frames = new List<string>();

            string[] segments = normalized.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                int offset = 0;

                while (offset < segment.Length)
                {
                    int length = Math.Min(
                        MaxFrameDataLength,
                        segment.Length - offset);

                    frames.Add(segment.Substring(offset, length));

                    offset += length;
                }
            }

            return frames;
        }

        private async Task LogCommunicationAsync(
            string direction,
            string description,
            byte[]? data = null)
        {
            string hex = data = null ? "" : Convert.ToHexString(data);
            string message = $"{DateTime.Now:HH:mm:ss:fff} "+ $"{direction, -3} {description}";

            if(!string.IsNullOrWhiteSpace(hex))
            {
                message += $" Data: {hex}";
            }

            await _hubContext.Clients.All.SendAsync("AstmLog", message);
        }
    }
