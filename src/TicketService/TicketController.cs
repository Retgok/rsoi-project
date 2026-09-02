using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;
using SharedEvents;

namespace TicketService;

[ApiController]
[Route("api/v1/tickets")]
[Authorize]
[Produces("application/json")]
public class TicketsController : ControllerBase
{
    private readonly ITicketRepo _repo;
    private readonly IEventPublisher _events;

    public TicketsController(ITicketRepo repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TicketResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var list = await _repo.GetAllByUserAsync(username);
        return Ok(list.Select(t => new TicketResponse(t)));
    }

    [HttpGet("{ticketUid:guid}")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByUid(Guid ticketUid)
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var ticket = await _repo.GetByUidAsync(ticketUid, username);
        if (ticket == null)
            return NotFound(new ErrorResponse($"Ticket {ticketUid} not found"));

        return Ok(new TicketResponse(ticket));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TicketPurchaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Purchase([FromBody] TicketPurchaseRequest dto)
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        if (!ModelState.IsValid)
            return ValidationProblem();

        var ticket = new Ticket
        {
            TicketUid = Guid.NewGuid(),
            Username = username,
            FlightNumber = dto.FlightNumber,
            Price = dto.Price,
            Status = "PAID"
        };

        await _repo.AddAsync(ticket);

        _events.Publish(new ServiceEvent(
            "ticket-service",
            "ticket_created",
            username,
            ticket.TicketUid.ToString(),
            DateTime.UtcNow));

        return Ok(new TicketPurchaseResponse(ticket, paidByMoney: dto.Price, paidByBonuses: 0));
    }

    [HttpDelete("{ticketUid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid ticketUid)
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var ticket = await _repo.GetByUidAsync(ticketUid, username);
        if (ticket == null)
            return NotFound(new ErrorResponse($"Ticket {ticketUid} not found"));

        ticket.Status = "CANCELED";
        await _repo.UpdateAsync(ticket);

        _events.Publish(new ServiceEvent(
            "ticket-service",
            "ticket_canceled",
            username,
            ticketUid.ToString(),
            DateTime.UtcNow));

        return NoContent();
    }

    public override ActionResult ValidationProblem()
    {
        var errors = ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => e.Key,
                e => string.Join(";", e.Value!.Errors.Select(er => er.ErrorMessage))
            );

        return BadRequest(new ValidationErrorResponse("Invalid data", errors));
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
