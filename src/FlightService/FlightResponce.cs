using System.Text.Json.Serialization;

namespace FlightService;

public class AirportResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;

    public AirportResponse() { }

    public AirportResponse(Airport a)
    {
        Id = a.Id;
        Name = a.Name;
        City = a.City;
        Country = a.Country;
    }
}

public class FlightResponse
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = default!;
    public string FromAirport { get; set; } = default!;
    public string ToAirport { get; set; } = default!;

    [JsonPropertyName("date")]
    public DateTime DateTime { get; set; }
    public int Price { get; set; }
    public int? Capacity { get; set; }

    public FlightResponse() { }

    public FlightResponse(Flight f)
    {
        Id = f.Id;
        FlightNumber = f.FlightNumber;
        FromAirport = $"{f.FromAirport!.City} {f.FromAirport.Name}";
        ToAirport = $"{f.ToAirport!.City} {f.ToAirport.Name}";
        DateTime = f.DateTime;
        Price = f.Price;
        Capacity = f.Capacity;
    }
}

public class CreateAirportRequest
{
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}

public class CreateFlightRequest
{
    public string FlightNumber { get; set; } = "";
    public DateTime DateTime { get; set; }
    public int FromAirportId { get; set; }
    public int ToAirportId { get; set; }
    public int Price { get; set; }
    public int? Capacity { get; set; }
}
