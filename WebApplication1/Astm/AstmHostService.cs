using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Net.Sockets;
using TCPMessageAPI.Hubs;

namespace TCPMessageAPI.Astm
{
    public class AstmHostService
    {
        private readonly AstmService _astmService;

        private TcpListener _listener;
        private CancellationTokenSource? _cancellation;
        private Task? _listenTask;

        public bool IsRunning { get; private set; }
        public bool IsAnalyserConnected => _astmService.IsConnected;

        public string? ConnectedAnalyser {  get; private set; }
        public int Port { get; private set; }

        public AstmHostService (AstmService astmService)
        {
            _astmService = astmService;
        }

        public async Task StartAsync(int port)
        {
            if (IsRunning)
                throw new InvalidOperationException("ASTM Host is already running.");

            if (port < 1 || port > 65535)
            {
                await _astmService.LogAsync(
                    "ERROR",
                    $"Invalid port: {port}. Port number must be between 1 and 65535.");

                throw new ArgumentOutOfRangeException(
                    nameof(port),
                    "Port number must be between 1 and 65535.");
            }

            try
            {
                Port = port;

                _cancellation = new CancellationTokenSource();

                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();

                IsRunning = true;

                _listenTask = ListenAsync(_cancellation.Token);

                await _astmService.LogAsync(
                    "INFO",
                    $"Host Started on port: {port}");
            }
            catch
            {
                IsRunning = false;
                _listener = null;
                _cancellation?.Dispose();
                _cancellation = null;
                throw;
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested, exit the loop
                        break;
                    }

                    if (_astmService.IsConnected)
                    {
                        // If already connected, reject the new connection
                        client.Close();
                        continue;
                    }

                    await HandleClientAsync(client, cancellationToken);
                }

            }
            catch (OperationCanceledException)
            {
                // Listener has been stopped, exit the loop
            }
            finally
            {
                IsRunning = false;
            }

        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                ConnectedAnalyser = client.Client.RemoteEndPoint?.ToString();

                await _astmService.AcceptConnectionAsync(client);

                while (_astmService.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(250, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if(_astmService.IsConnected)
                {
                    _astmService.Disconnect();
                }

                ConnectedAnalyser = null;
            }
        }

        public async Task StopAsync()
        {
            _cancellation?.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch 
            {
                await _astmService.LogAsync(
                    "ERROR",
                    $"Stopping host failed");
            }

            if( _listener != null )
            {
                try
                {
                    await _listenTask;
                }
                catch (OperationCanceledException)
                {
                    await _astmService.LogAsync(
                    "ERROR",
                    $"Stopping host failed");
                }
            }

            _listener = null;
            _listenTask = null;

            _cancellation?.Dispose();
            _cancellation = null;

            IsRunning = false;
            ConnectedAnalyser = null;

            await _astmService.LogAsync(
                    "INFO",
                    $"Host Stopped");
        }
    }
}
