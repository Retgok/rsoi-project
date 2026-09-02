using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;

namespace ApiGatewayService;

[ApiController]
[Route("api/v1/privilege")]
[Authorize]
[Produces("application/json")]
public class PrivilegeGatewayController : ControllerBase
{
    private readonly BonusService _bonus;

    public PrivilegeGatewayController(BonusService bonus)
    {
        _bonus = bonus;
    }

    [HttpGet]
    public async Task<IActionResult> GetPrivilege()
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        try
        {
            var privilege = await _bonus.GetPrivilegeCriticalAsync(username);
            if (privilege == null)
                return StatusCode(503, new { message = "Bonus Service unavailable" });

            return Ok(privilege);
        }
        catch
        {
            return StatusCode(503, new { message = "Bonus Service unavailable" });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        try
        {
            var privilege = await _bonus.GetPrivilegeCriticalAsync(username);
            if (privilege == null)
                return StatusCode(503, new { message = "Bonus Service unavailable" });

            return Ok(privilege.History);
        }
        catch
        {
            return StatusCode(503, new { message = "Bonus Service unavailable" });
        }
    }
}
