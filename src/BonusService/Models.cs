using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BonusService;

public class Privilege
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Username { get; set; } = default!;

    [Required]
    [RegularExpression("BRONZE|SILVER|GOLD")]
    public string Status { get; set; } = "BRONZE";

    public int Balance { get; set; }

    public List<PrivilegeHistory> History { get; set; } = new();
}

public class PrivilegeHistory
{
    public int Id { get; set; }

    [ForeignKey("Privilege")]
    public int PrivilegeId { get; set; }
    public Privilege Privilege { get; set; } = default!;

    public Guid TicketUid { get; set; }

    public DateTime DateTime { get; set; }

    public int BalanceDiff { get; set; }

    [Required]
    [RegularExpression("FILL_IN_BALANCE|DEBIT_THE_ACCOUNT")]
    public string OperationType { get; set; } = default!;
}
