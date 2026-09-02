namespace TicketService;

public class TicketPurchaseResponse : TicketResponse
{
    public int PaidByMoney { get; set; }
    public int PaidByBonuses { get; set; }


    public TicketPurchaseResponse(Ticket t, int paidByMoney, int paidByBonuses) : base(t)
    {
        PaidByMoney = paidByMoney;
        PaidByBonuses = paidByBonuses;
    }
}