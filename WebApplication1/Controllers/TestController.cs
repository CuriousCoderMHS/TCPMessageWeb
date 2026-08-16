using Microsoft.AspNetCore.Mvc;

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
    }
}
