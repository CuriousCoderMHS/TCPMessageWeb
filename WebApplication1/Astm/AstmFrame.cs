namespace TCPMessageAPI.Astm
{
    public class AstmFrame
    {
        public int FrameNumber { get; set; }
        public string Data { get; set; } = "";

        public bool IsLastFrame { get; set; }

        public string Checksum { get; set; } = "";
    }
}
