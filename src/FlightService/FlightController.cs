using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;
using SharedEvents;

namespace FlightService;

[ApiController]
[Route("api/v1/flights")]
[Authorize]
[Produces("application/json")]
public class FlightsController : ControllerBase
{
    private readonly IFlightRepo _repo;
    private readonly IEventPublisher _events;

    public FlightsController(IFlightRepo repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<FlightResponse>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int size = 100)
    {
        var list = await _repo.GetAllAsync(page, size);
        _events.Publish(new ServiceEvent(
            "flight-service",
            "flights_list",
            User.GetUsername(),
            $"page={page}",
            DateTime.UtcNow));
        return Ok(list.Select(f => new FlightResponse(f)));
    }

    [HttpGet("{flightNumber}")]
    [ProducesResponseType(typeof(FlightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByFlightNumber([FromRoute] string flightNumber)
    {
        var flight = await _repo.GetByFlightNumberAsync(flightNumber);
        if (flight == null)
            return NotFound(new ErrorResponse($"Flight {flightNumber} not found"));

        return Ok(new FlightResponse(flight));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(FlightResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateFlightRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FlightNumber) || request.Price <= 0)
            return BadRequest(new ErrorResponse("flightNumber and price > 0 are required"));

        if (request.FromAirportId == request.ToAirportId)
            return BadRequest(new ErrorResponse("fromAirportId and toAirportId must differ"));

        if (request.Capacity is <= 0)
            return BadRequest(new ErrorResponse("capacity must be > 0 when provided"));

        if (await _repo.GetByFlightNumberAsync(request.FlightNumber) != null)
            return Conflict(new ErrorResponse($"Flight {request.FlightNumber} already exists"));

        var from = await _repo.GetAirportByIdAsync(request.FromAirportId);
        var to = await _repo.GetAirportByIdAsync(request.ToAirportId);
        if (from == null || to == null)
            return BadRequest(new ErrorResponse("Airport not found"));

        var flight = await _repo.AddFlightAsync(new Flight
        {
            FlightNumber = request.FlightNumber.Trim(),
            DateTime = request.DateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.DateTime, DateTimeKind.Utc)
                : request.DateTime.ToUniversalTime(),
            FromAirportId = request.FromAirportId,
            ToAirportId = request.ToAirportId,
            Price = request.Price,
            Capacity = request.Capacity
        });

        _events.Publish(new ServiceEvent(
            "flight-service",
            "flight_created",
            User.GetUsername(),
            flight.FlightNumber,
            DateTime.UtcNow));

        return Created($"/api/v1/flights/{flight.FlightNumber}", new FlightResponse(flight));
    }
}

[ApiController]
[Route("api/v1/airports")]
[Authorize]
[Produces("application/json")]
public class AirportsController : ControllerBase
{
    private readonly IFlightRepo _repo;
    private readonly IEventPublisher _events;

    public AirportsController(IFlightRepo repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var airports = await _repo.GetAirportsAsync();
        return Ok(airports.Select(a => new AirportResponse(a)));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateAirportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.City)
            || string.IsNullOrWhiteSpace(request.Country))
            return BadRequest(new ErrorResponse("name, city and country are required"));

        var airport = await _repo.AddAirportAsync(new Airport
        {
            Name = request.Name.Trim(),
            City = request.City.Trim(),
            Country = request.Country.Trim()
        });

        _events.Publish(new ServiceEvent(
            "flight-service",
            "airport_created",
            User.GetUsername(),
            airport.Id.ToString(),
            DateTime.UtcNow));

        return Created($"/api/v1/airports/{airport.Id}", new AirportResponse(airport));
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
