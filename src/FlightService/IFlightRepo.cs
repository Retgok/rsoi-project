namespace FlightService;

public interface IFlightRepo
{
    Task<List<Flight>> GetAllAsync(int page, int size);
    Task<Flight?> GetByFlightNumberAsync(string flightNumber);
    Task<Flight> AddFlightAsync(Flight flight);
    Task<List<Airport>> GetAirportsAsync();
    Task<Airport?> GetAirportByIdAsync(int id);
    Task<Airport> AddAirportAsync(Airport airport);
}
