namespace TicketService;

public interface ITicketRepo
{
    Task<List<Ticket>> GetAllByUserAsync(string username);
    Task<Ticket?> GetByUidAsync(Guid uid, string username);
    Task<Ticket> AddAsync(Ticket ticket);
    Task UpdateAsync(Ticket ticket);
    Task DeleteAsync(Ticket ticket);
}