using System.Net;
using System.Net.Http.Json;

namespace ApiGatewayService;

public class BonusClient
{
    private readonly HttpClient _client;

    public BonusClient(HttpClient client) => _client = client;

    public async Task<ApplyBonusResponse?> ApplyAsync(string username, ApplyBonusRequest reqDto)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/privilege/apply", reqDto);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ApplyBonusResponse>();
    }

    public async Task<RefundResult> RefundAsync(string username, Guid ticketUid)
    {
        var resp = await _client.PostAsync($"/api/v1/privilege/refund/{ticketUid}", null);

        if (resp.IsSuccessStatusCode)
            return RefundResult.Success;

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return RefundResult.NotNeeded;

        return RefundResult.Retry;
    }

    public async Task<PrivilegeInfoResponse?> GetPrivilegeAsync(string username)
    {
        var resp = await _client.GetAsync("/api/v1/privilege");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PrivilegeInfoResponse>();
    }
}
