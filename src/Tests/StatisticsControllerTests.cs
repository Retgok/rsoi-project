using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatisticsService;
using TestsSupport;

namespace Tests;

public class StatisticsControllerTests
{
    private static StatisticsDb CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<StatisticsDb>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new StatisticsDb(options);
        db.Events.AddRange(
            new EventLogEntry
            {
                ServiceName = "api-gateway",
                Action = "ticket_purchase",
                Username = "ivan",
                Details = "test",
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            },
            new EventLogEntry
            {
                ServiceName = "ticket-service",
                Action = "ticket.create",
                Username = "admin",
                CreatedAt = DateTime.UtcNow
            },
            new EventLogEntry
            {
                ServiceName = "api-gateway",
                Action = "request_metrics",
                Username = "ivan",
                Details = """{"path":"/api/v1/flights","method":"GET","status":200,"db_ms":0,"db_queries":0}""",
                DurationMs = 120,
                CreatedAt = DateTime.UtcNow
            },
            new EventLogEntry
            {
                ServiceName = "bonus-service",
                Action = "request_metrics",
                Details = """{"path":"/api/v1/privilege","method":"GET","status":200,"db_ms":35,"db_queries":2}""",
                DurationMs = 80,
                CreatedAt = DateTime.UtcNow
            },
            new EventLogEntry
            {
                ServiceName = "api-gateway",
                Action = "http_error",
                Details = """{"path":"/api/v1/tickets","method":"POST","status":503,"db_ms":0,"db_queries":0}""",
                DurationMs = 250,
                CreatedAt = DateTime.UtcNow
            });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task GetReport_ReturnsAggregatedData_ForAdmin()
    {
        var db = CreateDb(nameof(GetReport_ReturnsAggregatedData_ForAdmin));
        var controller = new StatisticsController(db);
        ControllerTestHelper.SetUser(controller, "admin", "Admin");

        var result = await controller.GetReport(null, null, null, null);
        var ok = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<StatisticsReport>(ok.Value);

        Assert.Equal(5, report.TotalEvents);
        Assert.Contains(report.ByService, g => g.Name == "api-gateway");
        Assert.Contains(report.ByAction, g => g.Name == "ticket_purchase");
        Assert.Equal(2, report.Load.TotalRequests);
        Assert.Equal(1, report.Load.TotalErrors);
        Assert.Equal(100, report.Performance.AvgHttpDurationMs);
        Assert.Equal(35, report.Performance.AvgDbDurationMs);
        Assert.NotEmpty(report.RecentEvents);
    }

    [Fact]
    public async Task GetReport_FiltersByDateRange()
    {
        var db = CreateDb(nameof(GetReport_FiltersByDateRange));
        var controller = new StatisticsController(db);
        ControllerTestHelper.SetUser(controller, "admin", "Admin");

        var result = await controller.GetReport(DateTime.UtcNow.AddMinutes(-30), null, null, null);
        var ok = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<StatisticsReport>(ok.Value);

        Assert.Equal(4, report.TotalEvents);
    }

    [Fact]
    public async Task GetReport_FiltersByServiceAndAction()
    {
        var db = CreateDb(nameof(GetReport_FiltersByServiceAndAction));
        var controller = new StatisticsController(db);
        ControllerTestHelper.SetUser(controller, "admin", "Admin");

        var result = await controller.GetReport(null, null, "api-gateway", "ticket_purchase");
        var ok = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<StatisticsReport>(ok.Value);

        Assert.Equal(1, report.TotalEvents);
        Assert.Equal("api-gateway", report.Service);
        Assert.Equal("ticket_purchase", report.Action);
    }

    [Fact]
    public async Task GetEvents_ReturnsPagedFilteredList()
    {
        var db = CreateDb(nameof(GetEvents_ReturnsPagedFilteredList));
        var controller = new StatisticsController(db);
        ControllerTestHelper.SetUser(controller, "admin", "Admin");

        var result = await controller.GetEvents(null, null, "api-gateway", null, null, null, 1, 10);
        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<EventsPage>(ok.Value);

        Assert.True(page.TotalElements >= 1);
        Assert.All(page.Items, item => Assert.Contains("api-gateway", item.ServiceName));
    }
}

public class StatisticsReportBuilderTests
{
    [Fact]
    public void Build_CalculatesPerformanceMetrics()
    {
        var events = new List<EventLogEntry>
        {
            new()
            {
                ServiceName = "flight-service",
                Action = "request_metrics",
                DurationMs = 100,
                Details = """{"db_ms":20,"db_queries":1}""",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                ServiceName = "flight-service",
                Action = "request_metrics",
                DurationMs = 200,
                Details = """{"db_ms":40,"db_queries":2}""",
                CreatedAt = DateTime.UtcNow
            }
        };

        var report = StatisticsReportBuilder.Build(events);

        Assert.Equal(150, report.Performance.AvgHttpDurationMs);
        Assert.Equal(30, report.Performance.AvgDbDurationMs);
        Assert.Single(report.Performance.ByService);
    }
}
