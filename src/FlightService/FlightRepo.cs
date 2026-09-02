using Microsoft.EntityFrameworkCore;

namespace FlightService;

public class FlightRepo : IFlightRepo
{
    private readonly FlightDb _db;

    public FlightRepo(FlightDb db) => _db = db;

    public async Task<List<Flight>> GetAllAsync(int page, int size)
    {
        return await _db.Flights
            .Include(f => f.FromAirport)
            .Include(f => f.ToAirport)
            .OrderByDescending(f => f.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();
    }

    public async Task<Flight?> GetByFlightNumberAsync(string flightNumber)
    {
        return await _db.Flights
            .Include(f => f.FromAirport)
            .Include(f => f.ToAirport)
            .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber);
    }

    public async Task<Flight> AddFlightAsync(Flight flight)
    {
        _db.Flights.Add(flight);
        await _db.SaveChangesAsync();
        return (await GetByFlightNumberAsync(flight.FlightNumber))!;
    }

    public async Task<List<Airport>> GetAirportsAsync()
        => await _db.Airports.OrderBy(a => a.Id).ToListAsync();

    public async Task<Airport?> GetAirportByIdAsync(int id)
        => await _db.Airports.FirstOrDefaultAsync(a => a.Id == id);

    public async Task<Airport> AddAirportAsync(Airport airport)
    {
        _db.Airports.Add(airport);
        await _db.SaveChangesAsync();
        return airport;
    }
}
