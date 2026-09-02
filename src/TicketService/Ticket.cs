namespace TicketService;

public class Ticket
{
    public int Id { get; set; }
    public Guid TicketUid { get; set; }
    public string Username { get; set; } = default!;
    public string FlightNumber { get; set; } = default!;
    public int Price { get; set; }
    public string Status { get; set; } = "PAID";
}