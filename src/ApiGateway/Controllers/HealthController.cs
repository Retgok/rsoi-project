using Microsoft.AspNetCore.Mvc;
namespace ApiGatewayService;

[ApiController]
[Route("manage")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok("OK");
    }
}