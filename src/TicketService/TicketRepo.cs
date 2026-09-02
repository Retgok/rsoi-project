using Microsoft.EntityFrameworkCore;

namespace TicketService;

public class TicketRepo : ITicketRepo
{
    private readonly TicketDb _db;
    public TicketRepo(TicketDb db) { _db = db; }


    public async Task<List<Ticket>> GetAllByUserAsync(string username) =>
    await _db.Tickets.Where(t => t.Username == username).ToListAsync();


    public async Task<Ticket?> GetByUidAsync(Guid uid, string username) =>
    await _db.Tickets.FirstOrDefaultAsync(t => t.TicketUid == uid && t.Username == username);


    public async Task<Ticket> AddAsync(Ticket ticket)
    {
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }


    public async Task UpdateAsync(Ticket ticket)
    {
        _db.Tickets.Update(ticket);
        await _db.SaveChangesAsync();
    }


    public async Task DeleteAsync(Ticket ticket)
    {
        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();
    }
}