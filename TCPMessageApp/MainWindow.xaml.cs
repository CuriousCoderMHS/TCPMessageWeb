using System.Windows;

namespace TCPMessageApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            AddLog("Application started.");
        }

        private void AddLog(string message)
        {
            CommunicationLogTextBox.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] {message}\r\n");

            CommunicationLogTextBox.ScrollToEnd();
        }

        private void ConnectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string host = HostTextBox.Text;
            string port = PortTextBox.Text;

            AddLog($"Connecting to {host}:{port}...");

            // TCP/API connection will be added next.
        }

        private void DisconnectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddLog("Disconnected.");
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

        private void SendButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddLog("Send requested.");

            // API communication will be added next.
        }
    }
}
