using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SharedEvents;

public sealed class RequestMetricsOptions
{
    public required string ServiceName { get; init; }
}

public sealed class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestMetricsOptions _options;

    public RequestMetricsMiddleware(RequestDelegate next, RequestMetricsOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context, IEventPublisher publisher)
    {
        if (ShouldSkip(context.Request.Path))
        {
            await _next(context);
            return;
        }

        RequestMetricsContext.Reset();
        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        var dbMetrics = RequestMetricsContext.Current;
        var details = JsonSerializer.Serialize(new
        {
            path = context.Request.Path.Value,
            method = context.Request.Method,
            status = context.Response.StatusCode,
            db_ms = dbMetrics.DbTimeMs,
            db_queries = dbMetrics.DbQueryCount
        });

        var action = context.Response.StatusCode >= 500 ? "http_error" : "request_metrics";
        publisher.Publish(new ServiceEvent(
            _options.ServiceName,
            action,
            GetUsername(context.User),
            details,
            DateTime.UtcNow,
            (int)stopwatch.ElapsedMilliseconds));

        RequestMetricsContext.Reset();
    }

    private static bool ShouldSkip(PathString path)
        => path.StartsWithSegments("/manage")
           || path.StartsWithSegments("/swagger")
           || path.StartsWithSegments("/.well-known");

    private static string? GetUsername(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirstValue("preferred_username")
               ?? user.FindFirstValue(ClaimTypes.Name)
               ?? user.FindFirstValue("sub");
    }
}
