using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet] 
        public ActionResult Get() {
            return Ok(new
            {
                message = "Hello, World!"
            });
        }
        [HttpPost("connect")]
        public IActionResult Connect(TcpConnectRequest request)
        {
            // Implementation for connecting to TCP server
            return Ok(new
            {
                message = $"Connecting to {request.IpAddress}:{request.Port}"
            });
        }
    }
}
