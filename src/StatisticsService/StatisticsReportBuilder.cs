using System.Text.Json;

namespace StatisticsService;

public static class StatisticsReportBuilder
{
    public static StatisticsReport Build(IReadOnlyList<EventLogEntry> events)
    {
        var metricEvents = events
            .Where(e => e.Action is "request_metrics" or "http_error")
            .ToList();

        var businessEvents = events
            .Where(e => e.Action is not "request_metrics" and not "http_error")
            .ToList();

        return new StatisticsReport
        {
            TotalEvents = events.Count,
            ByService = GroupCounts(events, e => e.ServiceName),
            ByAction = GroupCounts(events, e => e.Action),
            ByUser = events
                .Where(e => !string.IsNullOrWhiteSpace(e.Username))
                .GroupBy(e => e.Username!)
                .Select(g => new CountGroup { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(20)
                .ToList(),
            RecentEvents = events
                .OrderByDescending(e => e.CreatedAt)
                .Take(50)
                .Select(MapRecentEvent)
                .ToList(),
            Load = BuildLoadMetrics(events, metricEvents),
            Performance = BuildPerformanceMetrics(metricEvents)
        };
    }

    private static LoadMetrics BuildLoadMetrics(
        IReadOnlyList<EventLogEntry> allEvents,
        IReadOnlyList<EventLogEntry> metricEvents)
    {
        var errors = metricEvents.Count(e => e.Action == "http_error");
        var requests = metricEvents.Count(e => e.Action == "request_metrics");

        return new LoadMetrics
        {
            TotalRequests = requests,
            TotalErrors = errors,
            ErrorRatePercent = requests == 0 ? 0 : Math.Round(errors * 100.0 / requests, 1),
            EventsByHour = allEvents
                .GroupBy(e => e.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:00"))
                .Select(g => new HourlyLoad { Hour = g.Key, Count = g.Count() })
                .OrderBy(x => x.Hour)
                .TakeLast(24)
                .ToList(),
            BusiestServices = allEvents
                .GroupBy(e => e.ServiceName)
                .Select(g => new CountGroup { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList()
        };
    }

    private static PerformanceMetrics BuildPerformanceMetrics(IReadOnlyList<EventLogEntry> metricEvents)
    {
        var parsed = metricEvents
            .Where(e => e.Action == "request_metrics")
            .Select(e => new ParsedMetric(e.ServiceName, e.DurationMs ?? 0, ParseDbMs(e.Details)))
            .Where(x => x.HttpMs > 0)
            .ToList();

        if (parsed.Count == 0)
            return new PerformanceMetrics();

        return new PerformanceMetrics
        {
            AvgHttpDurationMs = Math.Round(parsed.Average(x => x.HttpMs), 1),
            AvgDbDurationMs = Math.Round(parsed.Where(x => x.DbMs > 0).DefaultIfEmpty().Average(x => x?.DbMs ?? 0), 1),
            MaxHttpDurationMs = parsed.Max(x => x.HttpMs),
            MaxDbDurationMs = parsed.Where(x => x.DbMs > 0).Select(x => x.DbMs).DefaultIfEmpty(0).Max(),
            ByService = parsed
                .GroupBy(x => x.ServiceName)
                .Select(g => new ServicePerformance
                {
                    ServiceName = g.Key,
                    RequestCount = g.Count(),
                    AvgHttpMs = Math.Round(g.Average(x => x.HttpMs), 1),
                    AvgDbMs = Math.Round(g.Where(x => x.DbMs > 0).DefaultIfEmpty().Average(x => x?.DbMs ?? 0), 1)
                })
                .OrderByDescending(x => x.RequestCount)
                .ToList()
        };
    }

    private static List<CountGroup> GroupCounts(
        IReadOnlyList<EventLogEntry> events,
        Func<EventLogEntry, string> selector)
        => events
            .GroupBy(selector)
            .Select(g => new CountGroup { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

    private static RecentEvent MapRecentEvent(EventLogEntry e)
        => new()
        {
            ServiceName = e.ServiceName,
            Action = e.Action,
            Username = e.Username,
            Details = e.Details,
            DurationMs = e.DurationMs,
            CreatedAt = e.CreatedAt
        };

    private static long ParseDbMs(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("db_ms", out var dbMs))
                return dbMs.GetInt64();
        }
        catch (JsonException)
        {
        }

        return 0;
    }

    private sealed record ParsedMetric(string ServiceName, int HttpMs, long DbMs);
}
