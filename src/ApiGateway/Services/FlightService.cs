namespace ApiGatewayService;

public class FlightService
{
    private readonly FlightsClient _client;
    private readonly ICircuitBreaker _breaker;

    public FlightService(FlightsClient client, ICircuitBreaker breaker)
    {
        _client = client;
        _breaker = breaker;
    }

    public async Task<PaginationResponse?> GetFlightsAsync(int page, int size)
    {
        return await _breaker.ExecuteAsync(
            action: () => _client.GetAllAsync(page, size),
            fallback: () => null,
            isCritical: true
        );
    }

    public async Task<FlightResponse?> GetByNumberAsync(
        string flightNumber,
        string username)
    {
        return await _breaker.ExecuteAsync(
            action: () => _client.GetByFlightNumberAsync(flightNumber, username),
            fallback: () => new FlightResponse
            {
                FlightNumber = flightNumber,
                FromAirport = "UNKNOWN",
                ToAirport = "UNKNOWN",
                Date = DateTime.MinValue,
                Price = 0
            },
            isCritical: false
        );
    }
}
