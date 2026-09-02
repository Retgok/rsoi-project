using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BonusService;

public class BonusRepo : IBonusRepo
{
    private readonly BonusDb _db;

    public BonusRepo(BonusDb db)
    {
        _db = db;
    }

    public async Task<Privilege?> GetByUsernameAsync(string username)
    {
        return await _db.Privileges
            .Include(p => p.History)
            .FirstOrDefaultAsync(p => p.Username == username);
    }

    public async Task AddPrivilegeAsync(Privilege privilege)
    {
        _db.Privileges.Add(privilege);
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePrivilegeAsync(Privilege privilege)
    {
        _db.Privileges.Update(privilege);
        await _db.SaveChangesAsync();
    }

    public async Task<PrivilegeHistory?> GetLastHistoryByTicketAsync(Guid ticketUid)
    {
        return await _db.PrivilegeHistories
            .Include(h => h.Privilege)
            .Where(h => h.TicketUid == ticketUid)
            .OrderByDescending(h => h.DateTime)
            .FirstOrDefaultAsync();
    }

    public async Task AddHistoryAsync(PrivilegeHistory history)
    {
        _db.PrivilegeHistories.Add(history);
        await _db.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
