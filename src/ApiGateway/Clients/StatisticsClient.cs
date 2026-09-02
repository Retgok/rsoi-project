using System.Net.Http;

namespace ApiGatewayService;

public class StatisticsClient
{
    private readonly HttpClient _client;

    public StatisticsClient(HttpClient client) => _client = client;

    public async Task<HttpResponseMessage> GetReportAsync(
        DateTime? from,
        DateTime? to,
        string? service,
        string? action)
    {
        var path = "/api/v1/statistics/report" + BuildQuery(from, to, service, action, null, null, null, null);
        return await _client.GetAsync(path);
    }

    public async Task<HttpResponseMessage> GetEventsAsync(
        DateTime? from,
        DateTime? to,
        string? service,
        string? action,
        string? username,
        string? query,
        int page,
        int size)
    {
        var path = "/api/v1/statistics/events"
                   + BuildQuery(from, to, service, action, username, query, page, size);
        return await _client.GetAsync(path);
    }

    private static string BuildQuery(
        DateTime? from,
        DateTime? to,
        string? service,
        string? action,
        string? username,
        string? query,
        int? page,
        int? size)
    {
        var parts = new List<string>();
        if (from.HasValue)
            parts.Add($"from={Uri.EscapeDataString(from.Value.ToUniversalTime().ToString("o"))}");
        if (to.HasValue)
            parts.Add($"to={Uri.EscapeDataString(to.Value.ToUniversalTime().ToString("o"))}");
        if (!string.IsNullOrWhiteSpace(service))
            parts.Add($"service={Uri.EscapeDataString(service)}");
        if (!string.IsNullOrWhiteSpace(action))
            parts.Add($"action={Uri.EscapeDataString(action)}");
        if (!string.IsNullOrWhiteSpace(username))
            parts.Add($"username={Uri.EscapeDataString(username)}");
        if (!string.IsNullOrWhiteSpace(query))
            parts.Add($"query={Uri.EscapeDataString(query)}");
        if (page.HasValue)
            parts.Add($"page={page.Value}");
        if (size.HasValue)
            parts.Add($"size={size.Value}");
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }
}
