namespace TCPMessageAPI.Astm
{
    public class AstmMessage
    {
        public List<AstmFrame> Frames { get; set; } = new();

        public string RawMessage => string.Join(Environment.NewLine, Frames.Select(f => f.Data));
    }
}
