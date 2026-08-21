using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace TCPMessageApp.Services
{
    public class AstmHubService
    {
        private HubConnection? _connection;

        public event Action<string>? LogReceived;

        public async Task ConnectAsync(string apiBaseUrl)
        {
            if (_connection != null)
                return;

            _connection = new HubConnectionBuilder()
                .WithUrl($"{apiBaseUrl}/astmHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string>(
                "AstmLog",
                message =>
                {
                    LogReceived?.Invoke(message);
                });

            _connection.Reconnecting += error =>
            {
                LogReceived?.Invoke(
                    $"SIGNALR RECONNECTING: {error?.Message}");

                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                LogReceived?.Invoke(
                    $"SIGNALR CONNECTED: {connectionId}");

                return Task.CompletedTask;
            };

            _connection.Closed += error =>
            {
                LogReceived?.Invoke(
                    $"SIGNALR CLOSED: {error?.Message}");

                return Task.CompletedTask;
            };

            await _connection.StartAsync();

            LogReceived?.Invoke(
                "SIGNALR CONNECTED");
        }

        public async Task DisconnectAsync()
        {
            if (_connection == null)
                return;

            await _connection.StopAsync();
            await _connection.DisposeAsync();

            _connection = null;
        }
    }
}
