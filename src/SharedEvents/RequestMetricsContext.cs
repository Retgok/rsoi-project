namespace SharedEvents;

public sealed class RequestMetricsSnapshot
{
    public long DbTimeMs { get; private set; }
    public int DbQueryCount { get; private set; }

    public void AddDbTime(long durationMs)
    {
        if (durationMs <= 0)
            return;

        DbTimeMs += durationMs;
        DbQueryCount++;
    }
}

public static class RequestMetricsContext
{
    private static readonly AsyncLocal<RequestMetricsSnapshot?> CurrentMetrics = new();

    public static RequestMetricsSnapshot Current => CurrentMetrics.Value ??= new RequestMetricsSnapshot();

    public static void Reset() => CurrentMetrics.Value = new RequestMetricsSnapshot();
}
