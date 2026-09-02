using ApiGatewayService;
using Microsoft.AspNetCore.Authorization;
using SharedAuth;
using SharedEvents;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAuthForwarding();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRequestMetrics("api-gateway");

builder.Services.AddHttpClient<FlightsClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:FlightService"] ?? "http://flight_service:8060");
}).AddAuthForwarding();

builder.Services.AddHttpClient<BonusClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:BonusService"] ?? "http://bonus_service:8050");
}).AddAuthForwarding();

builder.Services.AddHttpClient<TicketsClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:TicketService"] ?? "http://ticket_service:8070");
}).AddAuthForwarding();

builder.Services.AddHttpClient<StatisticsClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:StatisticsService"] ?? "http://statistics_service:8040");
}).AddAuthForwarding();

builder.Services.AddHttpClient<IdentityClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:IdentityProvider"] ?? "http://identity_provider:8090");
}).AddAuthForwarding();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IBonusRefundQueue, BonusRefundQueue>();
builder.Services.AddHostedService<BonusRefundWorker>();
builder.Services.AddKafkaEventPublisher();
builder.Services.AddServiceJwtAuth(builder.Configuration);

builder.Services.AddSingleton<ICircuitBreaker>(
    _ => new CircuitBreaker(failureThreshold: 3, openTimeout: TimeSpan.FromSeconds(15)));

builder.Services.AddScoped<FlightService>();
builder.Services.AddScoped<TicketsService>();
builder.Services.AddScoped<BonusService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "GatewayAPI";
    config.Title = "GatewayAPI v1";
    config.Version = "v1";
});

var app = builder.Build();

app.UseOpenApi();
app.UseSwaggerUi();
app.UseRouting();
app.UseServiceJwtAuth();
app.UseRequestMetrics();
app.MapControllers();
app.Run();
