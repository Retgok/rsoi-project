namespace FlightService;

public class Flight
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = "";
    public DateTime DateTime { get; set; }
    public int FromAirportId { get; set; }
    public int ToAirportId { get; set; }
    public int Price { get; set; }
    public int? Capacity { get; set; }

    public Airport? FromAirport { get; set; }
    public Airport? ToAirport { get; set; }
}