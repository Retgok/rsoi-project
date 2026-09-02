using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;
using SharedEvents;

namespace ApiGatewayService;

[ApiController]
[Route("api/v1/flights")]
[Authorize]
[Produces("application/json")]
public class FlightsGatewayController : ControllerBase
{
    private readonly FlightService _service;
    private readonly FlightsClient _client;
    private readonly IEventPublisher _events;

    public FlightsGatewayController(FlightService service, FlightsClient client, IEventPublisher events)
    {
        _service = service;
        _client = client;
        _events = events;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFlights(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        if (size < 1 || size > 100)
            return BadRequest(new { message = "Invalid paging parameters" });

        try
        {
            var result = await _service.GetFlightsAsync(page, size);
            if (result == null)
                return StatusCode(500, new { message = "Flight service unavailable" });

            _events.Publish(new ServiceEvent(
                "api-gateway",
                "flights_list",
                User.GetUsername(),
                $"page={page}",
                DateTime.UtcNow));

            return Ok(result);
        }
        catch
        {
            return StatusCode(500, new { message = "Flight service unavailable" });
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateFlight([FromBody] CreateFlightRequest request)
    {
        var response = await _client.CreateFlightAsync(request);
        return await ForwardAsync(response);
    }

    private static async Task<IActionResult> ForwardAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new UnauthorizedResult();
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new ForbidResult();
        if (string.IsNullOrWhiteSpace(body))
            return new StatusCodeResult((int)response.StatusCode);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = body,
            ContentType = "application/json"
        };
    }
}

[ApiController]
[Route("api/v1/airports")]
[Authorize]
[Produces("application/json")]
public class AirportsGatewayController : ControllerBase
{
    private readonly FlightsClient _client;

    public AirportsGatewayController(FlightsClient client) => _client = client;

    [HttpGet]
    public async Task<IActionResult> GetAirports()
    {
        var response = await _client.GetAirportsAsync();
        return await ForwardAsync(response);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateAirport([FromBody] CreateAirportRequest request)
    {
        var response = await _client.CreateAirportAsync(request);
        return await ForwardAsync(response);
    }

    private static async Task<IActionResult> ForwardAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new UnauthorizedResult();
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new ForbidResult();
        if (string.IsNullOrWhiteSpace(body))
            return new StatusCodeResult((int)response.StatusCode);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = body,
            ContentType = "application/json"
        };
    }
}
