using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedAuth;

namespace ApiGatewayService;

[ApiController]
[Route("api/v1/statistics")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public class StatisticsGatewayController : ControllerBase
{
    private readonly StatisticsClient _client;

    public StatisticsGatewayController(StatisticsClient client)
    {
        _client = client;
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? service,
        [FromQuery] string? action)
    {
        var response = await _client.GetReportAsync(from, to, service, action);
        return await ForwardAsync(response);
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? service,
        [FromQuery] string? action,
        [FromQuery] string? username,
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var response = await _client.GetEventsAsync(from, to, service, action, username, query, page, size);
        return await ForwardAsync(response);
    }

    private static async Task<IActionResult> ForwardAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new UnauthorizedResult();

        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new ForbidResult();

        if (!response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(body))
            return new ObjectResult(new { message = "Statistics Service unavailable" })
            {
                StatusCode = (int)response.StatusCode
            };

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = string.IsNullOrWhiteSpace(body) ? "{}" : body,
            ContentType = "application/json"
        };
    }
}
