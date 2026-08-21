using TCPMessageApp.Services;
using System;
using System.Windows;
using System.Windows.Media;

namespace TCPMessageApp
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;

        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

            AddLog("Application started.");
        }

        private void AddLog(string message)
        {
            CommunicationLogTextBox.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] {message}\r\n");

            CommunicationLogTextBox.ScrollToEnd();
        }

        private async void ConnectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string host = HostTextBox.Text.Trim();

                if (!int.TryParse(PortTextBox.Text.Trim(), out int port))
                {
                    MessageBox.Show("Invalid port number.", "Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AddLog($"Connecting to {host}:{port}...");

                ConnectButton.IsEnabled = false;

                await _apiService.ConnectAstmAsync(host, port);
                AddLog("ASTM connected.");

                UpdateConnectionStatus(true);
            }
            catch (Exception ex)
            {
                AddLog($"Error connecting: {ex.Message}");

                UpdateConnectionStatus(false);

                MessageBox.Show(ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        private async void DisconnectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                await _apiService.DisconnectAstmAsync();

                AddLog("Disconnected.");

                UpdateConnectionStatus(false);
            }
            catch (Exception ex)
            {
                AddLog($"Disconnect failed: {ex.Message}");
            }
        }

        private void ClearLogButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            CommunicationLogTextBox.Clear();
        }

        private void BuildAstmButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OutgoingMessageTextBox.Text =
                "H|\\^&|||Analyzer|||||||P\r\n" +
                "P|1\r\n" +
                "O|1|123456||^^^GLU\r\n" +
                "R|1|^^^GLU|5.4|mmol/L\r\n" +
                "L|1|N";

            AddLog("ASTM message built.");
        }

        private async void SendButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string message = OutgoingMessageTextBox.Text;

            if(string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Message is empty.", "Send Message", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SendButton.IsEnabled = false;

                AddLog($"Sending message:\r\n{message}");

                await _apiService.SendAstmAsync(message);

                AddLog("Message sent successfully.");

            }
            catch (Exception ex)
            {
                AddLog($"Error sending message: {ex.Message}");

                MessageBox.Show(ex.Message, "Send Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private void UpdateConnectionStatus(bool connected)
        {
            if (connected)
            {
                StatusTextBlock.Text = "Connected";
                StatusTextBlock.Foreground =
                    new SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            else
            {
                StatusTextBlock.Text = "Disconnected";
                StatusTextBlock.Foreground =
                    new SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }
    }
}
