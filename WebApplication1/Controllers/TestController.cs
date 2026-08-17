using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly TcpService _tcpService;

        public TestController(TcpService tcpService)
        {
            _tcpService = tcpService;
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
    }
}
