using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using TCPMessageAPI.Astm;

namespace TCPMessageAPI.Tests
{
    public class AstmServiceBuildFrameTests
    {
        [Fact]
        public void BuildFrame_ProducesValidFrame_ParseSucceedsAndChecksumMatches()
        {
            // Arrange
            var service = new AstmService(null, null);
            var method = typeof(AstmService).GetMethod("BuildFrame", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            string data = "PATIENT|1";

            // Act
            var frameObj = method.Invoke(service, new object[] { data, 0, true });
            Assert.NotNull(frameObj);

            var frame = (byte[])frameObj;

            // Assert basic structure
            Assert.Equal(AstmConstants.STX, frame[0]);
            Assert.Equal((byte)'0', frame[1]);

            int terminatorIndex = Array.IndexOf(frame, AstmConstants.ETX, 2);
            Assert.True(terminatorIndex > 1, "ETX not found in frame");

            // checksum bytes follow terminator
            int checksumIndex = terminatorIndex + 1;
            string checksumHex = Encoding.ASCII.GetString(frame, checksumIndex, 2);
            byte calculated = AstmFrameParser.CalculateChecksum(frame, terminatorIndex);
            Assert.Equal(calculated.ToString("X2"), checksumHex, ignoreCase: true);

            // CR LF at end
            Assert.Equal(AstmConstants.CR, frame[frame.Length - 2]);
            Assert.Equal(AstmConstants.LF, frame[frame.Length - 1]);

            // Parser should accept the frame
            var parsed = AstmFrameParser.Parse(frame);
            Assert.Equal(0, parsed.FrameNumber);
            Assert.Equal(data, parsed.Data);
            Assert.True(parsed.IsLastFrame);
        }

        [Fact]
        public void BuildFrame_WithFrameNumber_OutOfRange_Throws()
        {
            var service = new AstmService(null, null);
            var method = typeof(AstmService).GetMethod("BuildFrame", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            // frameNumber 8 is out of allowed range 0..7 and should throw ArgumentOutOfRangeException
            Assert.Throws<TargetInvocationException>(() => method.Invoke(service, new object[] { "X", 8, true }));
        }
    }
}
