namespace TicketService;

public class TicketPurchaseRequest
{
    public string FlightNumber { get; set; } = default!;
    public int Price { get; set; }
    public bool PaidFromBalance { get; set; } = false;
}