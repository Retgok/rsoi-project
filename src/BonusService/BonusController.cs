using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;
using SharedEvents;

namespace BonusService;

[ApiController]
[Route("api/v1/privilege")]
[Authorize]
[Produces("application/json")]
public class BonusController : ControllerBase
{
    private readonly IBonusRepo _repo;
    private readonly IEventPublisher _events;

    public BonusController(IBonusRepo repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var privilege = await _repo.GetByUsernameAsync(username);
        if (privilege == null)
        {
            privilege = new Privilege { Username = username, Balance = 0, Status = "BRONZE" };
            await _repo.AddPrivilegeAsync(privilege);
        }

        return Ok(new PrivilegeResponse(privilege));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var privilege = await _repo.GetByUsernameAsync(username);
        if (privilege == null)
            return Ok(Array.Empty<PrivilegeHistoryResponse>());

        return Ok(privilege.History.Select(h => new PrivilegeHistoryResponse(h)));
    }

    [HttpPost("apply")]
    public async Task<IActionResult> ApplyBonus([FromBody] ApplyBonusRequest req)
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var privilege = await _repo.GetByUsernameAsync(username);
        if (privilege == null)
        {
            privilege = new Privilege { Username = username, Balance = 0, Status = "BRONZE" };
            await _repo.AddPrivilegeAsync(privilege);
        }

        int usedBonus = 0;
        int paidByMoney = req.Price;

        if (req.PaidFromBalance)
        {
            usedBonus = Math.Min(privilege.Balance, req.Price);
            paidByMoney -= usedBonus;
            privilege.Balance -= usedBonus;

            await _repo.AddHistoryAsync(new PrivilegeHistory
            {
                PrivilegeId = privilege.Id,
                TicketUid = req.TicketUid,
                DateTime = DateTime.UtcNow,
                BalanceDiff = -usedBonus,
                OperationType = "DEBIT_THE_ACCOUNT"
            });
        }
        else
        {
            int bonus = (int)(req.Price * 0.1);
            privilege.Balance += bonus;

            await _repo.AddHistoryAsync(new PrivilegeHistory
            {
                PrivilegeId = privilege.Id,
                TicketUid = req.TicketUid,
                DateTime = DateTime.UtcNow,
                BalanceDiff = bonus,
                OperationType = "FILL_IN_BALANCE"
            });
        }

        privilege.Status = privilege.Balance switch
        {
            >= 10000 => "GOLD",
            >= 5000 => "SILVER",
            _ => "BRONZE"
        };

        await _repo.UpdatePrivilegeAsync(privilege);

        _events.Publish(new ServiceEvent(
            "bonus-service",
            req.PaidFromBalance ? "bonus_debit" : "bonus_credit",
            username,
            req.TicketUid.ToString(),
            DateTime.UtcNow));

        return Ok(new { paidByMoney, paidByBonuses = usedBonus });
    }

    [HttpPost("refund/{ticketUid:guid}")]
    public async Task<IActionResult> Refund(Guid ticketUid)
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var lastHistory = await _repo.GetLastHistoryByTicketAsync(ticketUid);
        if (lastHistory == null)
            return NotFound(new { error = "No history found for ticket" });

        var privilege = lastHistory.Privilege;

        int delta = lastHistory.OperationType switch
        {
            // BalanceDiff хранится со знаком: DEBIT = отрицательный, FILL = положительный
            "DEBIT_THE_ACCOUNT" => -lastHistory.BalanceDiff,
            "FILL_IN_BALANCE" => -lastHistory.BalanceDiff,
            "FILLED_BY_MONEY" => 0,
            _ => 0
        };

        if (delta == 0)
            return NoContent();

        privilege.Balance += delta;
        if (privilege.Balance < 0) privilege.Balance = 0;

        await _repo.AddHistoryAsync(new PrivilegeHistory
        {
            PrivilegeId = privilege.Id,
            TicketUid = ticketUid,
            DateTime = DateTime.UtcNow,
            BalanceDiff = Math.Abs(delta),
            OperationType = delta > 0 ? "FILL_IN_BALANCE" : "DEBIT_THE_ACCOUNT"
        });

        privilege.Status = privilege.Balance switch
        {
            >= 10000 => "GOLD",
            >= 5000 => "SILVER",
            _ => "BRONZE"
        };

        await _repo.UpdatePrivilegeAsync(privilege);

        _events.Publish(new ServiceEvent(
            "bonus-service",
            "bonus_refund",
            username,
            ticketUid.ToString(),
            DateTime.UtcNow));

        return NoContent();
    }
}

[ApiController]
[Route("manage")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok("OK");
}

public class ApplyBonusRequest
{
    public Guid TicketUid { get; set; }
    public int Price { get; set; }
    public bool PaidFromBalance { get; set; }
}
