using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;

namespace ApiGatewayService;

[ApiController]
[Route("api/v1/me")]
[Authorize]
[Produces("application/json")]
public class UserInfoGatewayController : ControllerBase
{
    private readonly TicketsService _tickets;
    private readonly BonusService _bonus;

    public UserInfoGatewayController(TicketsService tickets, BonusService bonus)
    {
        _tickets = tickets;
        _bonus = bonus;
    }

    [HttpGet]
    [ProducesResponseType(typeof(UserInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserInfo()
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var tickets = await _tickets.GetAllAsync(username);
        if (tickets == null)
            return StatusCode(503, new ErrorResponse("Ticket Service unavailable"));

        var privilege = await _bonus.GetPrivilegeSafeAsync(username);

        return Ok(new UserInfoResponse
        {
            Tickets = tickets,
            Privilege = privilege
        });
    }
}
