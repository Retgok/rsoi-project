using Microsoft.EntityFrameworkCore;
using SharedAuth;
using SharedEvents;
using FlightService;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
var connectionString = Environment.GetEnvironmentVariable("DB_CONN_STR")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddRequestMetrics("flight-service");
builder.Services.AddDbContext<FlightDb>((sp, options) =>
    options.UseNpgsql(connectionString).AddInterceptors(sp.GetRequiredService<DbMetricsInterceptor>()));
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddScoped<IFlightRepo, FlightRepo>();
builder.Services.AddKafkaEventPublisher();
builder.Services.AddServiceJwtAuth(builder.Configuration);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);

builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "FlightServiceAPI";
    config.Title = "FlightServiceAPI v1";
    config.Version = "v1";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<FlightDb>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE flight ADD COLUMN IF NOT EXISTS capacity INT;");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("FlightMigration");
        logger.LogWarning(ex, "Could not migrate flight.capacity automatically");
    }
}

app.UseOpenApi();
app.UseSwaggerUi();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (System.Text.Json.JsonException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ValidationErrorResponse(
            "Invalid data",
            new Dictionary<string, string> { { "body", ex.Message } }));
    }
});
app.UseServiceJwtAuth();
app.UseRequestMetrics();
app.MapControllers();
app.Run();
