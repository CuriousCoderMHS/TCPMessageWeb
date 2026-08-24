namespace TCPMessageApp.Models
{
    public class LogMessage
    {
        public DateTime Timestamp { get; set;  }
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
