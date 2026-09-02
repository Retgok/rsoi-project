namespace BonusService;

public class PrivilegeHistoryResponse
{
    public DateTime Date { get; set; }
    public Guid TicketUid { get; set; }
    public int BalanceDiff { get; set; }
    public string OperationType { get; set; } = default!;

    public PrivilegeHistoryResponse(PrivilegeHistory h)
    {
        Date = h.DateTime;
        TicketUid = h.TicketUid;
        BalanceDiff = h.BalanceDiff;
        OperationType = h.OperationType;
    }
}

public class PrivilegeResponse
{
    public int Balance { get; set; }
    public string Status { get; set; } = default!;
    public List<PrivilegeHistoryResponse> History { get; set; } = new();

    public PrivilegeResponse(Privilege p)
    {
        Balance = p.Balance;
        Status = p.Status;
        History = p.History.Select(h => new PrivilegeHistoryResponse(h)).ToList();
    }
}