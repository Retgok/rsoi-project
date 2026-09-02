using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;
using SharedEvents;

namespace ApiGatewayService;

[ApiController]
[Route("api/v1/tickets")]
[Authorize]
[Produces("application/json")]
public class TicketsGatewayController : ControllerBase
{
    private readonly TicketsService _service;
    private readonly IEventPublisher _events;

    public TicketsGatewayController(TicketsService service, IEventPublisher events)
    {
        _service = service;
        _events = events;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAll()
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        try
        {
            var tickets = await _service.GetAllAsync(username);
            if (tickets == null)
                return StatusCode(503, new { message = "Ticket service unavailable" });

            _events.Publish(new ServiceEvent("api-gateway", "tickets_list", username, null, DateTime.UtcNow));
            return Ok(tickets);
        }
        catch
        {
            return StatusCode(503, new { message = "Ticket service unavailable" });
        }
    }

    [HttpGet("{ticketUid:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetByUid(Guid ticketUid)
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        try
        {
            var ticket = await _service.GetByUidAsync(ticketUid, username);
            if (ticket == null)
                return NotFound(new { message = $"Ticket {ticketUid} not found" });

            return Ok(ticket);
        }
        catch
        {
            return StatusCode(503, new { message = "Ticket service unavailable" });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(TicketPurchaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Purchase([FromBody] TicketPurchaseRequest dto)
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        if (dto == null || string.IsNullOrWhiteSpace(dto.FlightNumber) || dto.Price <= 0)
            return BadRequest(new ValidationErrorResponse(
                "Invalid request",
                new()
                {
                    { "flightNumber", "required" },
                    { "price", "must be > 0" }
                }
            ));

        var result = await _service.PurchaseAsync(username, dto);
        if (result == null)
            return StatusCode(503, new ErrorResponse("Bonus Service unavailable"));

        _events.Publish(new ServiceEvent(
            "api-gateway",
            "ticket_purchased",
            username,
            dto.FlightNumber,
            DateTime.UtcNow));

        return Ok(result);
    }

    [HttpDelete("{ticketUid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Cancel(Guid ticketUid)
    {
        var username = User.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var success = await _service.CancelAsync(
            ticketUid,
            username,
            Request.Headers.Authorization.ToString());
        if (!success)
            return StatusCode(503, new ErrorResponse("Ticket service unavailable"));

        _events.Publish(new ServiceEvent(
            "api-gateway",
            "ticket_canceled",
            username,
            ticketUid.ToString(),
            DateTime.UtcNow));

        return NoContent();
    }
}
