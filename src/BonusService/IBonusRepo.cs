using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BonusService;

public interface IBonusRepo
{
    Task<Privilege?> GetByUsernameAsync(string username);
    Task AddPrivilegeAsync(Privilege privilege);
    Task UpdatePrivilegeAsync(Privilege privilege);
    Task<PrivilegeHistory?> GetLastHistoryByTicketAsync(Guid ticketUid);
    Task AddHistoryAsync(PrivilegeHistory history);
    Task SaveChangesAsync();
}
