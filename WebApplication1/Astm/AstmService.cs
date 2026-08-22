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

        private readonly IHubContext<AstmHub> _hubContext;

        private CancellationTokenSource? _receiveCancellation;
        private Task? _receiveTask;

        // Only one ASTM transmission may be active at a time.
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        // The receive loop completes this when ACK/NAK arrives.
        private TaskCompletionSource<byte>? _responseWaiter;

        private const int MaxRetries = 6;

        // Confirm against the analyser's ASTM profile.
        private const int MaxFrameDataLength = 240;

        public AstmService(
            IHubContext<AstmHub> hubContext)
        {
            _hubContext = hubContext;
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

                    return !(socket.Poll(
                                 1000,
                                 SelectMode.SelectRead)
                             && socket.Available == 0);
                }
                catch
                {
                    return false;
                }
            }
        }

        // ============================================================
        // CONNECTION
        // ============================================================

        public async Task ConnectAsync(
            string ipAddress,
            int port)
        {
            Disconnect();

            var client = new TcpClient();

            try
            {
                await client.ConnectAsync(
                    ipAddress,
                    port);

                _client = client;
                _stream = client.GetStream();

                await LogCommunicationAsync(
                    "SYS",
                    $"Connected to {ipAddress}:{port}");

                StartReceiving();
            }
            catch
            {
                client.Dispose();

                _client = null;
                _stream = null;

                throw;
            }
        }

        public void Disconnect()
        {
            try
            {
                _receiveCancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _stream?.Close();
                _stream?.Dispose();
            }
            catch
            {
            }

            try
            {
                _client?.Close();
                _client?.Dispose();
            }
            catch
            {
            }

            _stream = null;
            _client = null;

            _receiveCancellation = null;
            _receiveTask = null;
            _responseWaiter = null;
        }

        // ============================================================
        // BACKGROUND RECEIVE
        // ============================================================

        public void StartReceiving()
        {
            if (_receiveTask != null &&
                !_receiveTask.IsCompleted)
            {
                return;
            }

            EnsureConnected();

            _receiveCancellation =
                new CancellationTokenSource();

            _receiveTask =
                ReceiveLoopAsync(
                    _receiveCancellation.Token);
        }

        private async Task ReceiveLoopAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte firstByte =
                        await ReadRawByteAsync(
                            cancellationToken);

                    // ------------------------------------------------
                    // ACK
                    // ------------------------------------------------

                    if (firstByte == AstmConstants.ACK)
                    {
                        await LogCommunicationAsync(
                            "RX",
                            "ACK",
                            "[06]");

                        _responseWaiter?
                            .TrySetResult(
                                AstmConstants.ACK);

                        continue;
                    }

                    // ------------------------------------------------
                    // NAK
                    // ------------------------------------------------

                    if (firstByte == AstmConstants.NAK)
                    {
                        await LogCommunicationAsync(
                            "RX",
                            "NAK",
                            "[15]");

                        _responseWaiter?
                            .TrySetResult(
                                AstmConstants.NAK);

                        continue;
                    }

                    // ------------------------------------------------
                    // ENQ
                    // ------------------------------------------------

                    if (firstByte == AstmConstants.ENQ)
                    {
                        await LogCommunicationAsync(
                            "RX",
                            "ENQ",
                            "[05]");

                        // Receiver grants permission.
                        await SendControlAsync(
                            AstmConstants.ACK);

                        continue;
                    }

                    // ------------------------------------------------
                    // EOT
                    // ------------------------------------------------

                    if (firstByte == AstmConstants.EOT)
                    {
                        await LogCommunicationAsync(
                            "RX",
                            "EOT",
                            "[04]");

                        continue;
                    }

                    // ------------------------------------------------
                    // ASTM FRAME
                    // ------------------------------------------------

                    if (firstByte == AstmConstants.STX)
                    {
                        byte[] frame =
                            await ReadFrameAfterStxAsync(
                                cancellationToken);

                        await ProcessReceivedFrameAsync(
                            frame);

                        continue;
                    }

                    // ------------------------------------------------
                    // Unexpected byte
                    // ------------------------------------------------

                    await LogCommunicationAsync(
                        "RX",
                        $"Unexpected byte 0x{firstByte:X2}");
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                await LogCommunicationAsync(
                    "ERR",
                    $"Receive loop stopped: {ex.Message}");
            }
        }

        // ============================================================
        // SEND ASTM MESSAGE
        // ============================================================

        public async Task SendMessageAsync(
            string message)
        {
            EnsureConnected();

            await _sendLock.WaitAsync();

            try
            {
                List<string> frames =
                    CreateFramePayloads(message);

                if (frames.Count == 0)
                {
                    throw new ArgumentException(
                        "ASTM message is empty.",
                        nameof(message));
                }

                // --------------------------------------------
                // ENQ
                // --------------------------------------------

                byte response =
                    await SendControlAndWaitAsync(
                        AstmConstants.ENQ,
                        TimeSpan.FromSeconds(10));

                if (response != AstmConstants.ACK)
                {
                    throw new InvalidOperationException(
                        $"ASTM receiver did not ACK ENQ. " +
                        $"Received 0x{response:X2}");
                }

                // --------------------------------------------
                // FRAMES
                // --------------------------------------------

                int frameNumber = 1;

                for (int i = 0;
                     i < frames.Count;
                     i++)
                {
                    bool lastFrame =
                        i == frames.Count - 1;

                    await SendFrameAsync(
                        frames[i],
                        frameNumber,
                        lastFrame);

                    frameNumber =
                        (frameNumber + 1) % 8;
                }

                // --------------------------------------------
                // EOT
                // --------------------------------------------

                await SendControlAsync(
                    AstmConstants.EOT);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // ============================================================
        // SEND FRAME
        // ============================================================

        private async Task SendFrameAsync(
            string data,
            int frameNumber,
            bool lastFrame)
        {
            byte[] frame =
                BuildFrame(
                    data,
                    frameNumber,
                    lastFrame);

            for (int attempt = 1;
                 attempt <= MaxRetries;
                 attempt++)
            {
                await LogCommunicationAsync(
                    "TX",
                    $"FRAME {frameNumber}" +
                    $" attempt {attempt}",
                    FormatAstmFrame(frame));

                await WriteAsync(frame);

                byte response =
                    await WaitForResponseAsync(
                        TimeSpan.FromSeconds(10));

                if (response == AstmConstants.ACK)
                {
                    return;
                }

                if (response == AstmConstants.NAK)
                {
                    await LogCommunicationAsync(
                        "SYS",
                        $"NAK received for frame " +
                        $"{frameNumber}; retrying.");

                    continue;
                }

                throw new InvalidOperationException(
                    $"Unexpected ASTM response: " +
                    $"0x{response:X2}");
            }

            throw new InvalidOperationException(
                $"ASTM frame {frameNumber} failed " +
                $"after {MaxRetries} attempts.");
        }

        // ============================================================
        // SEND CONTROL BYTE
        // ============================================================

        private async Task SendControlAsync(
            byte value)
        {
            await LogCommunicationAsync(
                "TX",
                GetControlCharacterName(value),
                $"[{value:X2}]");

            await WriteAsync(
                new[] { value });
        }

        private async Task<byte> SendControlAndWaitAsync(
            byte value,
            TimeSpan timeout)
        {
            await SendControlAsync(value);

            return await WaitForResponseAsync(
                timeout);
        }

        // ============================================================
        // WAIT FOR ACK / NAK
        // ============================================================

        private async Task<byte> WaitForResponseAsync(
            TimeSpan timeout)
        {
            var waiter =
                new TaskCompletionSource<byte>(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

            _responseWaiter = waiter;

            try
            {
                using var timeoutCts =
                    new CancellationTokenSource(timeout);

                return await waiter.Task.WaitAsync(
                    timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    "Timed out waiting for ASTM " +
                    "ACK/NAK.");
            }
            finally
            {
                if (ReferenceEquals(
                        _responseWaiter,
                        waiter))
                {
                    _responseWaiter = null;
                }
            }
        }

        // ============================================================
        // RECEIVE FRAME
        // ============================================================

        private async Task<byte[]> ReadFrameAfterStxAsync(
            CancellationToken cancellationToken)
        {
            var frame =
                new List<byte>
                {
                    AstmConstants.STX
                };

            while (true)
            {
                byte value =
                    await ReadRawByteAsync(
                        cancellationToken);

                frame.Add(value);

                if (value == AstmConstants.ETX ||
                    value == AstmConstants.ETB)
                {
                    break;
                }
            }

            // Two ASCII checksum characters.
            frame.Add(
                await ReadRawByteAsync(
                    cancellationToken));

            frame.Add(
                await ReadRawByteAsync(
                    cancellationToken));

            // CR
            frame.Add(
                await ReadRawByteAsync(
                    cancellationToken));

            // LF
            frame.Add(
                await ReadRawByteAsync(
                    cancellationToken));

            return frame.ToArray();
        }

        private async Task ProcessReceivedFrameAsync(
            byte[] frame)
        {
            try
            {
                AstmFrame astmFrame =
                    AstmFrameParser.Parse(frame);

                await LogCommunicationAsync(
                    "RX",
                    $"FRAME {astmFrame.FrameNumber}",
                    FormatAstmFrame(frame));

                await SendControlAsync(
                    AstmConstants.ACK);
            }
            catch (Exception ex)
            {
                await LogCommunicationAsync(
                    "ERR",
                    $"Invalid ASTM frame: {ex.Message}",
                    FormatAstmFrame(frame));

                await SendControlAsync(
                    AstmConstants.NAK);
            }
        }

        // ============================================================
        // LOW LEVEL TCP
        // ============================================================

        private async Task<byte> ReadRawByteAsync(
            CancellationToken cancellationToken)
        {
            EnsureConnected();

            byte[] buffer = new byte[1];

            int bytesRead =
                await _stream!.ReadAsync(
                    buffer,
                    cancellationToken);

            if (bytesRead == 0)
            {
                throw new InvalidOperationException(
                    "ASTM TCP connection was closed.");
            }

            return buffer[0];
        }

        private async Task WriteAsync(
            byte[] data)
        {
            EnsureConnected();

            await _stream!.WriteAsync(data);

            await _stream.FlushAsync();
        }

        private void EnsureConnected()
        {
            if (_client == null ||
                _stream == null ||
                !IsConnected)
            {
                throw new InvalidOperationException(
                    "ASTM not connected to a TCP server.");
            }
        }

        // ============================================================
        // FRAME CREATION
        // ============================================================

        private static byte[] BuildFrame(
            string data,
            int frameNumber,
            bool lastFrame)
        {
            if (frameNumber < 0 ||
                frameNumber > 7)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameNumber));
            }

            byte terminator =
                lastFrame
                    ? AstmConstants.ETX
                    : AstmConstants.ETB;

            byte[] dataBytes =
                Encoding.ASCII.GetBytes(data);

            var frame =
                new List<byte>
                {
                    AstmConstants.STX,
                    (byte)('0' + frameNumber)
                };

            frame.AddRange(dataBytes);

            frame.Add(terminator);

            byte checksum =
                CalculateChecksum(
                    frame.ToArray());

            frame.AddRange(
                Encoding.ASCII.GetBytes(
                    checksum.ToString("X2")));

            frame.Add(AstmConstants.CR);
            frame.Add(AstmConstants.LF);

            return frame.ToArray();
        }

        private static byte CalculateChecksum(
            byte[] frame)
        {
            int checksum = 0;

            // Frame number through ETX/ETB.
            // STX is excluded.
            for (int i = 1;
                 i < frame.Length;
                 i++)
            {
                checksum += frame[i];
            }

            return (byte)(checksum & 0xFF);
        }

        // ============================================================
        // FRAME PAYLOAD SPLITTING
        // ============================================================

        private static List<string>
            CreateFramePayloads(string message)
        {
            if (message.Any(
                    c => c > 0x7F))
            {
                throw new ArgumentException(
                    "ASTM messages must contain " +
                    "ASCII characters only.",
                    nameof(message));
            }

            string normalized =
                message
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');

            var frames =
                new List<string>();

            string[] records =
                normalized.Split(
                    '\n',
                    StringSplitOptions
                        .RemoveEmptyEntries);

            foreach (string record in records)
            {
                int offset = 0;

                while (offset < record.Length)
                {
                    int length =
                        Math.Min(
                            MaxFrameDataLength,
                            record.Length - offset);

                    frames.Add(
                        record.Substring(
                            offset,
                            length));

                    offset += length;
                }
            }

            return frames;
        }

        // ============================================================
        // LOGGING
        // ============================================================

        private async Task LogCommunicationAsync(
            string direction,
            string description,
            string? data = null)
        {
            string message =
                $"{DateTime.Now:HH:mm:ss.fff}  " +
                $"{direction,-3} " +
                $"{description}";

            if (!string.IsNullOrWhiteSpace(data))
            {
                message += $"  {data}";
            }

            await _hubContext.Clients.All.SendAsync(
                "AstmLog",
                message);
        }

        private static string
            GetControlCharacterName(byte value)
        {
            return value switch
            {
                AstmConstants.ENQ => "ENQ",
                AstmConstants.ACK => "ACK",
                AstmConstants.NAK => "NAK",
                AstmConstants.EOT => "EOT",
                AstmConstants.STX => "STX",
                AstmConstants.ETX => "ETX",
                AstmConstants.ETB => "ETB",
                AstmConstants.CR => "CR",
                AstmConstants.LF => "LF",
                _ => $"0x{value:X2}"
            };
        }

        private static string
            FormatAstmFrame(byte[] frame)
        {
            var sb =
                new StringBuilder();

            foreach (byte b in frame)
            {
                if (b >= 0x20 && b <= 0x7E)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append(
                        $"<{GetControlCharacterName(b)}>");
                }
            }

            return sb.ToString();
        }

        public async Task AcceptConnectionAsync(TcpClient client)
        {
            Disconnect();

            _client = client;
            _stream = client.GetStream();

            string remote = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

            await LogCommunicationAsync("SYS", $"Analyser connected: {remote}");

            StartReceiving();
        }
    }
}
