using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TCPMessageApp.Services;
using TCPMessenger;

namespace TCPMessageApp
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly AstmHubService _hubService;
        private enum MessageFormat { Hl7Mllp, AstmE1381 }
        private MessageFormat _format = MessageFormat.AstmE1381;

        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            _hubService = new AstmHubService();

            _hubService.LogReceived += HubLogReceived;

            Loaded += MainWindow_Loaded;

            AddLog("Application started.");
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _hubService.ConnectAsync("https://localhost:7222");

                AddLog("Live communication monitor connected.");
            }
            catch (Exception ex)
            {
                AddLog($"Error connecting to live monitor: {ex.Message}");
            }
        }

        private void AddLog(string message)
        {
            CommunicationLogTextBox.AppendText(
                message + Environment.NewLine);

            CommunicationLogTextBox.ScrollToEnd();
        }

        private async void HostButton_Click(object sender, RoutedEventArgs e)
        {
            if(HostButton.Content?.ToString() == "Start Host")
            {
                try
                {
                    HostButton.IsEnabled = false;

                    if (!int.TryParse(
                            HostPortTextBox.Text,
                            out int port))
                    {
                        MessageBox.Show(
                            "Enter a valid host port.",
                            "Host Mode",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    await _apiService.StartAstmHostAsync(
                        port);
                    UpdateHostStatus(true);

                    HostButton.Content = "Stop Host";

                }
                catch (Exception ex)
                {
                    //AddLog($"Connection failed: {ex.Message}");

                    MessageBox.Show(
                        ex.Message,
                        "Connection failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    HostButton.IsEnabled = true;

                    UpdateHostStatus(false);
                }
                finally
                {
                    HostButton.IsEnabled = true;
                }
            }
            else if (HostButton.Content?.ToString() == "Stop Host")
            {
                HostButton.IsEnabled = false;

                try
                {
                    await _apiService.StopAstmHostAsync();
                    HostButton.Content = "Start Host";
                    UpdateHostStatus(false);
                    
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.Message,
                        "Disconnection failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    HostButton.IsEnabled = true;

                    UpdateHostStatus(false);
                }
                finally
                {
                    HostButton.IsEnabled = true;
                }
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectButton.IsEnabled = false;

                await ConnectClientAsync();
            }
            catch (Exception ex)
            {
                AddLog(
                    $"Connection failed: {ex.Message}");

                MessageBox.Show(
                    ex.Message,
                    "Connection failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ConnectButton.IsEnabled = true;
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        private async Task ConnectClientAsync()
        {
            string ip =
                HostTextBox.Text;

            if (!int.TryParse(
                    PortTextBox.Text,
                    out int port))
            {
                throw new Exception(
                    "Enter a valid TCP port.");
            }

            await _apiService.ConnectAstmAsync(
                ip,
                port);

            UpdateConnectionStatus(true);
        }



        private async void DisconnectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                await _apiService.DisconnectAstmAsync();

                UpdateConnectionStatus(false);
            }
            catch (Exception ex)
            {
                
            }
        }

        private void ClearLogButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            CommunicationLogTextBox.Clear();
        }

        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export ASTM Log",
                    Filter =
                        "Log files (*.log)|*.log|" +
                        "Text files (*.txt)|*.txt|" +
                        "All files (*.*)|*.*",
                    DefaultExt = ".log",
                    FileName =
                        $"Logfile_{DateTime.Now:yyyyMMdd_HHmmss}.log"
                };

                if (dialog.ShowDialog() != true)
                    return;

                File.WriteAllText(dialog.FileName, CommunicationLogTextBox.Text);

                MessageBox.Show($"Log file export: {dialog.FileName}", "Export Log", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export log:\n\n{ex.Message}", "Export Log", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        private void ProtocolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _format = ProtocolComboBox.SelectedIndex == 0
                ? MessageFormat.AstmE1381
                : MessageFormat.Hl7Mllp;

            if (IsLoaded)
                UpdateBuildMessageButton();

        }

        private void UpdateBuildMessageButton()
        {
            if (_format == MessageFormat.AstmE1381)
            {
                BuildMessageButton.Content = "Build ASTM";
            }
            else
            {
                BuildMessageButton.Content = "Build HL7";
            }
        }
        private void BuildMessage_Click(object sender, RoutedEventArgs e)
        {
            string[] currentRecords = OutgoingMessageTextBox.Text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            if (_format == MessageFormat.AstmE1381)
            {
                var builder = new AstmMessageBuilderWindow(currentRecords)
                {
                    Owner = this
                };

                if (builder.ShowDialog() != true)
                    return;

                ProtocolComboBox.SelectedIndex = 0;
                _format = MessageFormat.AstmE1381;
                OutgoingMessageTextBox.Text = builder.GeneratedMessage;
            }
            else if (_format == MessageFormat.Hl7Mllp)
            {
                var builder = new Hl7MessageBuilderWindow(currentRecords)
                {
                    Owner = this
                };

                if (builder.ShowDialog() != true)
                    return;

                ProtocolComboBox.SelectedIndex = 1;
                _format = MessageFormat.Hl7Mllp;
                OutgoingMessageTextBox.Text = builder.GeneratedMessage;
            }
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
                StatusElipse.Fill = new SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            else
            {
                StatusTextBlock.Text = "Disconnected";
                StatusTextBlock.Foreground =
                    new SolidColorBrush(System.Windows.Media.Colors.Red);
                StatusElipse.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private void UpdateHostStatus(bool Started)
        {
            if (Started)
            {
                StatusTextBlock.Text = "Host Started";
                StatusTextBlock.Foreground =
                    new SolidColorBrush(System.Windows.Media.Colors.Green);
                StatusElipse.Fill = new SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            else
            {
                StatusTextBlock.Text = "Host Stopped";
                StatusTextBlock.Foreground =
                    new SolidColorBrush(System.Windows.Media.Colors.Red);
                StatusElipse.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private void HubLogReceived(string logMessage)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog(logMessage);
            });
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HostSettingsPanel == null)
                return;

            if (StatusTextBlock.Text == "Connected")
            {
                ModeComboBox.SelectedIndex = 0;
                MessageBox.Show("Cannot change Mode whilst connected to host", "Disconnect to switch mode", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StatusTextBlock.Text == "Host Started")
            {
                ModeComboBox.SelectedIndex = 1;
                MessageBox.Show("Cannot change Mode whilst host started", "Stop host to switch mode", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool hostMode = ModeComboBox.SelectedIndex == 1;

            HostSettingsPanel.Visibility = hostMode ? Visibility.Visible : Visibility.Hidden;
            ClientIPSettingsPanel.Visibility = hostMode ? Visibility.Hidden : Visibility.Visible;
            ClientPortSettingsPanel.Visibility = hostMode ? Visibility.Hidden : Visibility.Visible;

            ClientButtonPanel.Visibility = hostMode ? Visibility.Hidden : Visibility.Visible;
            HostButtonPanel.Visibility = hostMode ? Visibility.Visible : Visibility.Hidden;

            if (hostMode)
            {
                HostSettingsPanel.Visibility = Visibility.Visible;
                HostButtonPanel.Visibility = Visibility.Visible;
                ClientIPSettingsPanel.Visibility = Visibility.Hidden;
                ClientPortSettingsPanel.Visibility = Visibility.Hidden;
                ClientButtonPanel.Visibility = Visibility.Hidden;
                UpdateHostStatus(false);
            }
            else
            {
                HostSettingsPanel.Visibility = Visibility.Hidden;
                HostButtonPanel.Visibility = Visibility.Hidden;
                ClientIPSettingsPanel.Visibility = Visibility.Visible;
                ClientPortSettingsPanel.Visibility = Visibility.Visible;
                ClientButtonPanel.Visibility = Visibility.Visible;
                UpdateConnectionStatus(false);
            }
        }
    }
}
