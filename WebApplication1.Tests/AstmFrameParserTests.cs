using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using TCPMessageAPI.Astm;

namespace TCPMessageAPI.Tests
{
    public class AstmFrameParserTests
    {
        [Fact]
        public void Parse_ValidFrame_ReturnsExpected()
        {
            string data = "PATIENT|1";
            var frame = new List<byte>();

            frame.Add(AstmConstants.STX);
            frame.Add((byte)'0');
            var dataBytes = Encoding.ASCII.GetBytes(data);
            frame.AddRange(dataBytes);
            frame.Add(AstmConstants.ETX);

            int terminatorIndex = 2 + dataBytes.Length;

            byte calculated = AstmFrameParser.CalculateChecksum(frame.ToArray(), terminatorIndex);
            var checksumHex = calculated.ToString("X2");
            frame.AddRange(Encoding.ASCII.GetBytes(checksumHex));
            frame.Add(AstmConstants.CR);
            frame.Add(AstmConstants.LF);

            var parsed = AstmFrameParser.Parse(frame.ToArray());

            Assert.Equal(0, parsed.FrameNumber);
            Assert.Equal(data, parsed.Data);
            Assert.True(parsed.IsLastFrame);
            Assert.Equal(checksumHex, parsed.Checksum, ignoreCase: true);
        }

        [Fact]
        public void Parse_InvalidChecksum_Throws()
        {
            string data = "ORDER|5";
            var frame = new List<byte>();
            frame.Add(AstmConstants.STX);
            frame.Add((byte)'1');
            var dataBytes = Encoding.ASCII.GetBytes(data);
            frame.AddRange(dataBytes);
            frame.Add(AstmConstants.ETX);

            int terminatorIndex = 2 + dataBytes.Length;
            byte calculated = AstmFrameParser.CalculateChecksum(frame.ToArray(), terminatorIndex);
            var checksumHex = calculated.ToString("X2");
            // corrupt checksum
            var bad = checksumHex == "00" ? "FF" : "00";
            frame.AddRange(Encoding.ASCII.GetBytes(bad));
            frame.Add(AstmConstants.CR);
            frame.Add(AstmConstants.LF);

            Assert.Throws<InvalidOperationException>(() => AstmFrameParser.Parse(frame.ToArray()));
        }

        [Fact]
        public void Parse_MissingStx_Throws()
        {
            string data = "X";
            var frame = new List<byte>();
            frame.Add((byte)'?');
            frame.Add((byte)'0');
            frame.AddRange(Encoding.ASCII.GetBytes(data));
            frame.Add(AstmConstants.ETX);
            int terminatorIndex = 2 + data.Length;
            byte calculated = AstmFrameParser.CalculateChecksum(frame.ToArray(), terminatorIndex);
            var checksumHex = calculated.ToString("X2");
            frame.AddRange(Encoding.ASCII.GetBytes(checksumHex));
            frame.Add(AstmConstants.CR);
            frame.Add(AstmConstants.LF);

            Assert.Throws<InvalidOperationException>(() => AstmFrameParser.Parse(frame.ToArray()));
        }

        [Fact]
        public void Parse_MissingTerminator_Throws()
        {
            string data = "Y";
            var frame = new List<byte>();
            frame.Add(AstmConstants.STX);
            frame.Add((byte)'2');
            frame.AddRange(Encoding.ASCII.GetBytes(data));
            // no ETX/ETB
            // attempt to calculate terminator index incorrectly -> expect parser to throw before checksum check
            var checksumHex = "00";
            frame.AddRange(Encoding.ASCII.GetBytes(checksumHex));
            frame.Add(AstmConstants.CR);
            frame.Add(AstmConstants.LF);

            Assert.Throws<InvalidOperationException>(() => AstmFrameParser.Parse(frame.ToArray()));
        }
    }
}
