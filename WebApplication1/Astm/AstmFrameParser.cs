using System.Text;

namespace WebApplication1.Astm
{
    public class AstmFrameParser
    {
        public static AstmFrame Parse(byte[] frame)
        {
            if (frame.Length < 7)
            {
                throw new InvalidOperationException("ASTM frame is too short");
            }

            if (frame[0] != AstmConstants.STX)
            {
                throw new InvalidOperationException("ASTM frame does not start with STX");
            }

            int frameNumber = frame[1] - '0';

            int terminatorIndex = Array.IndexOf(frame, AstmConstants.ETX, 2);

            bool isLastFrame = true;

            if (terminatorIndex < 0)
            {
                terminatorIndex = Array.IndexOf(frame, AstmConstants.ETB, 2);
                isLastFrame = false;
            }

            if (terminatorIndex < 0)
            {
                throw new InvalidOperationException("ASTM frame does not contain ETX or ETB");
            }

            int checksumIndex = terminatorIndex + 1;

            if (checksumIndex + 2 >= frame.Length)
            {
                throw new InvalidOperationException("ASTM frame is missing checksum");
            }

            string checksum = Encoding.ASCII.GetString(frame, checksumIndex, 2);
            
            byte calculatedChecksum = CalculateChecksum(frame, terminatorIndex);

            string calculated = calculatedChecksum.ToString("X2");

            if (!checksum.Equals(calculated, StringComparison.OrdinalIgnoreCase))
            {
                string receivedHex = Convert.ToHexString(frame);
                throw new InvalidOperationException($"ASTM frame checksum mismatch. Expected: {checksum}, Calculated: {calculated}" + $" Frame bytes: {receivedHex}");
            }

            string data = Encoding.ASCII.GetString(frame, 2, terminatorIndex - 2);

            return new AstmFrame
            {
                FrameNumber = frameNumber,
                Data = data,
                IsLastFrame = isLastFrame,
                Checksum = checksum
            };

        }

        public static byte CalculateChecksum(byte[] frame, int terminatorIndex)
        {
            int checksum = 0;
            for (int i = 1; i <= terminatorIndex; i++)
            {
                checksum += frame[i];
            }
            return (byte)(checksum & 0xFF);
        }
    }
}
