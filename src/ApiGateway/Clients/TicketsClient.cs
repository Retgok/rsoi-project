using System.Net;
using System.Net.Http.Json;

namespace ApiGatewayService;

public class TicketsClient
{
    private readonly HttpClient _client;

    public TicketsClient(HttpClient client) => _client = client;

    public async Task<List<TicketResponse>?> GetAllByUserAsync(string username)
    {
        var resp = await _client.GetAsync("/api/v1/tickets");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<List<TicketResponse>>();
    }

    public async Task<TicketResponse?> GetByUidAsync(Guid ticketUid, string username)
    {
        var resp = await _client.GetAsync($"/api/v1/tickets/{ticketUid}");
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TicketResponse>();
    }

    public async Task<TicketPurchaseResponse?> PurchaseAsync(string username, TicketPurchaseRequest reqDto)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/tickets", reqDto);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<TicketPurchaseResponse>();
    }

    public async Task<bool> CancelAsync(Guid ticketUid, string username)
    {
        var resp = await _client.DeleteAsync($"/api/v1/tickets/{ticketUid}");
        return resp.IsSuccessStatusCode;
    }
}
