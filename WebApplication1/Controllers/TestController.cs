using Microsoft.AspNetCore.Mvc;
using TCPMessageAPI.Models;
using TCPMessageAPI.Services;
using TCPMessageAPI.Astm;

namespace TCPMessageAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly TcpService _tcpService;
        private readonly AstmService _astmService;

        public TestController(TcpService tcpService, AstmService astmService)
        {
            _tcpService = tcpService;
            _astmService = astmService;
        }

        [HttpGet]
        public IActionResult Get() {
            return Ok(new
            {
                message = "Hello, World!"
            });
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect(TcpConnectRequest request)
        {
            try
            {
                await _tcpService.ConnectAsync(
                    request.IpAddress,
                    request.Port);

                return Ok(new
                { MessageProcessingHandler = "TCP connection successful",
                    ipAddress = request.IpAddress,
                    ISupportExternalScope = request.Port
                });
            }

            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "TCP connection failed",
                    error = ex.Message
                });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                connected = _tcpService.IsConnected()
            });
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send(TcpSendRequest request)
        {
            try
            {
                await _tcpService.SendAsync(request.Message);
                return Ok(new
                {
                    message = "Message sent successfully",
                    data = request.Message
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Failed to send message",
                    error = ex.Message
                });
            }
        }

        [HttpGet("receive")]
        public async Task<IActionResult> Receive()
        {
            try
            {
                string message = await _tcpService.ReceiveAsync();
                return Ok(new
                {
                    message = message
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Failed to receive message",
                    error = ex.Message
                });
            }
        }

        [HttpPost("disconnect")]
        public IActionResult Disconnect()
        {
            try
            {
                _tcpService.Disconnect();

                return Ok(new
                {
                    message = "Disconnected from TCP server"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Error disconnecting from TCP server",
                    error = ex.Message
                });
            }
        }

        [HttpPost("astm/connect")]
        public async Task<IActionResult> ConnectAstm(TcpConnectRequest request)
        {
            try
            {
                await _astmService.ConnectAsync(
                    request.IpAddress,
                    request.Port);

                return Ok(new
                {
                    message = "ASTM connection successful",
                    ipAddress = request.IpAddress,
                    port = request.Port
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "ASTM connection failed",
                    error = ex.Message
                });
            }
        }

        [HttpGet("astm/status")]
        public IActionResult AstmStatus()
        {
            return Ok(new
            {
                connected = _astmService.IsConnected
            });
        }

        [HttpPost("astm/disconnect")]
        public IActionResult DisconnectAstm()
        {
            try
            {
                _astmService.Disconnect();

                return Ok(new
                {
                    message = "Disconnected from ASTM server"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Error disconnecting from ASTM server",
                    error = ex.Message
                });
            }
        }

        [HttpGet("astm/receive")]
        public async Task<IActionResult> ReceiveAstmFrame()
        {
            try
            {
                AstmMessage message = await _astmService.ReceiveMessageAsync();

                return Ok(new
                {
                    message = message.RawMessage,
                    frames = message.Frames.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Failed to receive ASTM frame",
                    error = ex.Message
                });
            }
        }

        [HttpPost("astm/send")]
        public async Task<IActionResult> SendAstm(AstmSendRequest request)
        {
            try
            {
                await _astmService.SendMessageAsync(request.Message);

                return Ok(new
                {
                    message = "ASTM message send successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Failed to send ASTM message",
                    error = ex.Message
                });
            }
        }
    }
}
