using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace ApiGatewayService;

public class FlightResponse
{
    public string FlightNumber { get; set; } = default!;
    public string FromAirport { get; set; } = default!;
    public string ToAirport { get; set; } = default!;
    
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    public int Price { get; set; }
    public int? Capacity { get; set; }
}

public class AirportResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
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

public class PaginationResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalElements { get; set; }
    public List<FlightResponse> Items { get; set; } = new();
}

public class UserInfoResponse
{
    public List<TicketResponse> Tickets { get; set; } = new();

    public PrivilegeShortInfo? Privilege { get; set; }
}

public class TicketResponse
{
    public Guid TicketUid { get; set; }
    public string FlightNumber { get; set; } = default!;
    public string FromAirport { get; set; } = default!;
    public string ToAirport { get; set; } = default!;
    public string Date { get; set; } = default!;
    public int Price { get; set; }
    public string Status { get; set; } = default!;
}

public class TicketPurchaseRequest
{
    public string FlightNumber { get; set; } = default!;
    public int Price { get; set; }
    public bool PaidFromBalance { get; set; }
}

public class TicketPurchaseResponse
{
    public Guid TicketUid { get; set; }
    public string FlightNumber { get; set; } = default!;
    public string FromAirport { get; set; } = default!;
    public string ToAirport { get; set; } = default!;
    public string Date { get; set; } = default!;
    public int Price { get; set; }
    public int PaidByMoney { get; set; }
    public int PaidByBonuses { get; set; }
    public string Status { get; set; } = default!;
    public PrivilegeShortInfo? Privilege { get; set; }
}

public class ApplyBonusRequest
{
    public Guid TicketUid { get; set; }
    public int Price { get; set; }
    public bool PaidFromBalance { get; set; }
}

public class ApplyBonusResponse
{
    public int PaidByMoney { get; set; }
    public int PaidByBonuses { get; set; }
}


public class PrivilegeInfoResponse
{
    public int Balance { get; set; }
    public string Status { get; set; } = default!;
    public List<BalanceHistory> History { get; set; } = new();
}

public class PrivilegeShortInfo
{
    public int Balance { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }
}

public class BalanceHistory
{
    public DateTime Date { get; set; }
    public Guid TicketUid { get; set; }
    public int BalanceDiff { get; set; }
    public string OperationType { get; set; } = default!;
}
