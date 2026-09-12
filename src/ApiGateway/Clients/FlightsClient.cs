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

    public async Task<bool?> TryIncrementBoughtAsync(string flightNumber)
    {
        var resp = await _client.PostAsync(
            $"/api/v1/flights/{Uri.EscapeDataString(flightNumber)}/bought/increment", null);
        if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            return false;
        if (!resp.IsSuccessStatusCode)
            return null;
        return true;
    }

    public async Task DecrementBoughtAsync(string flightNumber)
        => await _client.PostAsync(
            $"/api/v1/flights/{Uri.EscapeDataString(flightNumber)}/bought/decrement", null);
}
