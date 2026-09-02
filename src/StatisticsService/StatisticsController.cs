using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedAuth;

namespace StatisticsService;

[ApiController]
[Route("api/v1/statistics")]
[Authorize(Policy = "AdminOnly")]
public class StatisticsController : ControllerBase
{
    private readonly StatisticsDb _db;

    public StatisticsController(StatisticsDb db)
    {
        _db = db;
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? service,
        [FromQuery] string? action)
    {
        var query = _db.Events.AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)
            query = query.Where(e => e.CreatedAt <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(service))
            query = query.Where(e => e.ServiceName == service);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action == action);

        var events = await query.ToListAsync();
        var report = StatisticsReportBuilder.Build(events);
        report.From = from;
        report.To = to;
        report.Service = service;
        report.Action = action;

        return Ok(report);
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
        if (page < 1 || size < 1 || size > 100)
            return BadRequest(new { message = "Invalid paging parameters" });

        var dbQuery = _db.Events.AsQueryable();
        if (from.HasValue)
            dbQuery = dbQuery.Where(e => e.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)
            dbQuery = dbQuery.Where(e => e.CreatedAt <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(service))
            dbQuery = dbQuery.Where(e => e.ServiceName.Contains(service));
        if (!string.IsNullOrWhiteSpace(action))
            dbQuery = dbQuery.Where(e => e.Action.Contains(action));
        if (!string.IsNullOrWhiteSpace(username))
            dbQuery = dbQuery.Where(e => e.Username != null && e.Username.Contains(username));
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLowerInvariant();
            dbQuery = dbQuery.Where(e =>
                e.ServiceName.ToLower().Contains(q)
                || e.Action.ToLower().Contains(q)
                || (e.Username != null && e.Username.ToLower().Contains(q))
                || (e.Details != null && e.Details.ToLower().Contains(q)));
        }

        var total = await dbQuery.CountAsync();
        var items = await dbQuery
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(e => new RecentEvent
            {
                ServiceName = e.ServiceName,
                Action = e.Action,
                Username = e.Username,
                Details = e.Details,
                DurationMs = e.DurationMs,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        return Ok(new EventsPage
        {
            Page = page,
            PageSize = size,
            TotalElements = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size),
            Items = items
        });
    }
}

public sealed class EventsPage
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalElements { get; set; }
    public int TotalPages { get; set; }
    public List<RecentEvent> Items { get; set; } = [];
}

public sealed class StatisticsReport
{
    public int TotalEvents { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Service { get; set; }
    public string? Action { get; set; }
    public List<CountGroup> ByService { get; set; } = [];
    public List<CountGroup> ByAction { get; set; } = [];
    public List<CountGroup> ByUser { get; set; } = [];
    public List<RecentEvent> RecentEvents { get; set; } = [];
    public LoadMetrics Load { get; set; } = new();
    public PerformanceMetrics Performance { get; set; } = new();
}

public sealed class LoadMetrics
{
    public int TotalRequests { get; set; }
    public int TotalErrors { get; set; }
    public double ErrorRatePercent { get; set; }
    public List<HourlyLoad> EventsByHour { get; set; } = [];
    public List<CountGroup> BusiestServices { get; set; } = [];
}

public sealed class HourlyLoad
{
    public string Hour { get; set; } = "";
    public int Count { get; set; }
}

public sealed class PerformanceMetrics
{
    public double AvgHttpDurationMs { get; set; }
    public double AvgDbDurationMs { get; set; }
    public int MaxHttpDurationMs { get; set; }
    public long MaxDbDurationMs { get; set; }
    public List<ServicePerformance> ByService { get; set; } = [];
}

public sealed class ServicePerformance
{
    public string ServiceName { get; set; } = "";
    public int RequestCount { get; set; }
    public double AvgHttpMs { get; set; }
    public double AvgDbMs { get; set; }
}

public sealed class CountGroup
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

public sealed class RecentEvent
{
    public string ServiceName { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Username { get; set; }
    public string? Details { get; set; }
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

[ApiController]
[Route("manage")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok("OK");
}
