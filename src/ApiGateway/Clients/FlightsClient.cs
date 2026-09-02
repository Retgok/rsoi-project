using System.Net.Http.Json;

namespace ApiGatewayService;

public class FlightsClient
{
    private readonly HttpClient _client;

    public FlightsClient(HttpClient client) => _client = client;

    public async Task<FlightResponse?> GetByFlightNumberAsync(string flightNumber, string username)
    {
        var resp = await _client.GetAsync($"/api/v1/flights/{Uri.EscapeDataString(flightNumber)}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<FlightResponse>();
    }

    public async Task<PaginationResponse?> GetAllAsync(int page = 0, int size = 10)
    {
        var resp = await _client.GetAsync($"/api/v1/flights?page={page}&size={size}");
        if (!resp.IsSuccessStatusCode) return null;

        var flights = await resp.Content.ReadFromJsonAsync<List<FlightResponse>>();
        if (flights == null) return null;

        return new PaginationResponse
        {
            Page = page,
            PageSize = size,
            TotalElements = flights.Count,
            Items = flights
        };
    }

    public async Task<HttpResponseMessage> CreateFlightAsync(CreateFlightRequest request)
        => await _client.PostAsJsonAsync("/api/v1/flights", request);

    public async Task<HttpResponseMessage> GetAirportsAsync()
        => await _client.GetAsync("/api/v1/airports");

    public async Task<HttpResponseMessage> CreateAirportAsync(CreateAirportRequest request)
        => await _client.PostAsJsonAsync("/api/v1/airports", request);
}
