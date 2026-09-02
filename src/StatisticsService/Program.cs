using Microsoft.EntityFrameworkCore;
using SharedAuth;
using SharedEvents;
using StatisticsService;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
var connectionString = Environment.GetEnvironmentVariable("DB_CONN_STR")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Host=postgres;Port=5432;Database=statistics;Username=program;Password=test";

builder.Services.AddRequestMetrics("statistics-service");
builder.Services.AddDbContext<StatisticsDb>((sp, options) =>
    options.UseNpgsql(connectionString).AddInterceptors(sp.GetRequiredService<DbMetricsInterceptor>()));
builder.Services.AddKafkaEventPublisher();
builder.Services.AddHostedService<KafkaConsumerWorker>();
builder.Services.AddServiceJwtAuth(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "StatisticsAPI";
    config.Title = "Statistics Service";
    config.Version = "v1";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<StatisticsDb>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE event_log ADD COLUMN IF NOT EXISTS duration_ms INT;");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StatisticsMigration");
        logger.LogWarning(ex, "Could not migrate event_log.duration_ms automatically; run as postgres superuser if needed");
    }
}

app.UseOpenApi();
app.UseSwaggerUi();
app.UseServiceJwtAuth();
app.UseRequestMetrics();
app.MapControllers();
app.Run();
