namespace TicketService;

public class TicketResponse
{
    public Guid TicketUid { get; set; }
    public string FlightNumber { get; set; } = default!;
    public int Price { get; set; }
    public string Status { get; set; } = default!;


    public TicketResponse(Ticket t)
    {
        TicketUid = t.TicketUid;
        FlightNumber = t.FlightNumber;
        Price = t.Price;
        Status = t.Status;
    }
}