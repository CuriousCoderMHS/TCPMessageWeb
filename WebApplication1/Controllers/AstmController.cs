using Microsoft.AspNetCore.Mvc;
using TCPMessageAPI.Models;
using TCPMessageAPI.Services;
using TCPMessageAPI.Astm;

namespace TCPMessageAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class astmController : ControllerBase
    {
        private readonly TcpService _tcpService;
        private readonly AstmService _astmService;
        private readonly AstmHostService _astmHostService;

        public astmController(TcpService tcpService, AstmService astmService, AstmHostService astmHostService)
        {
            _tcpService = tcpService;
            _astmService = astmService;
            _astmHostService = astmHostService;
        }

        [HttpPost("connect")]
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

        [HttpGet("status")]
        public IActionResult AstmStatus()
        {
            return Ok(new
            {
                connected = _astmService.IsConnected
            });
        }

        [HttpPost("disconnect")]
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

        [HttpPost("send")]
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

        [HttpPost("host/start")]
        public async Task<IActionResult> StartAstmHost(
            [FromQuery] int port = 5000)
        {
            try
            {
                await _astmHostService.StartAsync(port);

                return Ok(new
                {
                    success = true,
                    mode = "host",
                    port,
                    status = "listening"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        [HttpPost("host/stop")]
        public async Task<IActionResult> StopAstmHost()
        {
            await _astmHostService.StopAsync();

            return Ok(new
            {
                success = true,
                mode = "host",
                status = "stopped"
            });
        }

        [HttpGet("host/status")]
        public IActionResult AstmHostStatus()
        {
            return Ok(new
            {
                running = _astmHostService.IsRunning,
                analyserConnected = _astmHostService.IsAnalyserConnected,
                port = _astmHostService.Port,
                analyser = _astmHostService.ConnectedAnalyser
            });
        }
    }
}
