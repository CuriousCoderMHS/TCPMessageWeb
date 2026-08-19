using System.Net.Sockets;
using System.Text;

namespace WebApplication1.Astm
{
    public class AstmService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        private const int TimeoutMilliseconds = 10000; // 10 seconds timeout
        private const int MaxRetires = 6;

        public bool IsConnected
        {
            get
            {
                if (_client == null)
                    return false;

                try
                {
                    Socket socket = _client.Client;

                    return !(socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0);
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

        public async Task SendFrameAsync(string data, int frameNumber, bool lastFrame = true)
        {
            EnsureConnected();

            byte[] frame = BuildFrame(data, frameNumber, lastFrame);

            for (int attempt = 0; attempt < MaxRetires; attempt++)
            {
                await _stream!.WriteAsync(frame);

                byte response = await ReadByteAsync();

                if (response == AstmConstants.ACK)
                {
                    return; // Frame sent successfully
                }
                else if (response == AstmConstants.NAK)
                {
                    // Retry sending the frame
                    continue;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unexpected ASTM response after sending frame: 0x{response:X2}");
                }
            }

            throw new InvalidOperationException("ASTM Failed to send frame after maximum attempts.");
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

        private byte[] BuildFrame(string data, int frameNumber, bool lastFrame)
        {
            if (frameNumber < 0 || frameNumber > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(frameNumber), "ASTM Frame number must be between 0 and 7.");
            }

            byte terminator = lastFrame ? AstmConstants.ETX : AstmConstants.ETB;

            byte[] dataBytes = Encoding.ASCII.GetBytes(data);

            var frame = new List<byte>();

            frame.Add(AstmConstants.STX);

            frame.Add((byte)('0' + frameNumber));

            frame.AddRange(dataBytes);

            frame.Add(terminator);

            byte checksum = CalculateChecksum(frame.ToArray());

            string checksumText = checksum.ToString("X2");

            frame.AddRange(Encoding.ASCII.GetBytes(checksumText));

            frame.Add(AstmConstants.CR);
            frame.Add(AstmConstants.LF);

            return frame.ToArray();

        }

        private static byte CalculateChecksum(byte[] frame)
        {
            int checksum = 0;

            // Calcaulate over frame number through ETX/ETB, excluding STX.
            for (int i = 0; i < frame.Length; i++)
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
                throw new InvalidOperationException("ASTM connection was closed.");
            }

            return buffer[0];
        }

        private void EnsureConnected()
        {
            if (!IsConnected || _stream == null)
            {
                throw new InvalidOperationException("ASTM Not connected to a TCP server.");
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
                    throw new InvalidOperationException("ASTM Connection closed by the device.");
                }
                start = await ReadByteAsync();
            }

            var frameBytes = new List<byte>()
            {
                AstmConstants.STX
            };

            while (true)
            {
                byte value = await ReadByteAsync();

                frameBytes.Add(value);

                if (value == AstmConstants.ETX || value == AstmConstants.ETB)
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

                throw new InvalidOperationException("ASTM Frame does not end with CR LF.");
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

                if (control == AstmConstants.STX)
                {
                    var frameBytes = new List<byte>()
                    {
                        AstmConstants.STX
                    };

                    while (true)
                    {
                        byte value = await ReadByteAsync();
                        frameBytes.Add(value);
                        if (value == AstmConstants.ETX || value == AstmConstants.ETB)
                        {
                            break;
                        }
                    }
                    // Read checksum and CR LF
                    frameBytes.Add(await ReadByteAsync());
                    frameBytes.Add(await ReadByteAsync());
                    frameBytes.Add(await ReadByteAsync());
                    frameBytes.Add(await ReadByteAsync());


                    try
                    {
                        AstmFrame frame = AstmFrameParser.Parse(frameBytes.ToArray());
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
        }

        public async Task SendMessageAsync(string message)
        {
            EnsureConnected();

            await SendByteAsync(AstmConstants.ENQ);

            byte response = await ReadByteAsync();

            if (response != AstmConstants.ACK)
            {
                throw new InvalidOperationException($"ASTM receiver did not ACK ENQ." + $"Received 0x{response:X2}");
            }

            string[] records = message.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

            int frameNumber = 1;

            foreach (string record in records)
            {
                await SendFrameAsync(record, frameNumber, true);

                frameNumber++;

                if (frameNumber > 7)
                {
                    frameNumber = 0;
                }
            }

            await SendByteAsync(AstmConstants.EOT);
        }
    }
}
