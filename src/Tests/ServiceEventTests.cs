using SharedEvents;

namespace Tests;

public class ServiceEventTests
{
    [Fact]
    public void ServiceEvent_StoresAllFields()
    {
        var timestamp = DateTime.UtcNow;
        var evt = new ServiceEvent("gateway", "ticket.purchase", "ivan", "flight AFL031", timestamp);

        Assert.Equal("gateway", evt.ServiceName);
        Assert.Equal("ticket.purchase", evt.Action);
        Assert.Equal("ivan", evt.Username);
        Assert.Equal("flight AFL031", evt.Details);
        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Null(evt.DurationMs);
    }

    [Fact]
    public void ServiceEvent_StoresDurationMs()
    {
        var evt = new ServiceEvent("gateway", "request_metrics", null, null, DateTime.UtcNow, 42);
        Assert.Equal(42, evt.DurationMs);
    }
}
