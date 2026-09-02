using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SharedEvents;

namespace StatisticsService;

public sealed class KafkaConsumerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KafkaConsumerWorker> _logger;

    public KafkaConsumerWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<KafkaConsumerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = _configuration["Kafka:BootstrapServers"] ?? "kafka:9092";
        var topic = _configuration["Kafka:Topic"] ?? "service-events";
        var groupId = _configuration["Kafka:GroupId"] ?? "statistics-service";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (string.IsNullOrWhiteSpace(result.Message.Value))
                    continue;

                var serviceEvent = JsonSerializer.Deserialize<ServiceEvent>(result.Message.Value);
                if (serviceEvent == null)
                    continue;

                await PersistEventAsync(serviceEvent, stoppingToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Failed to persist service event");
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Kafka consume error");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PersistEventAsync(ServiceEvent serviceEvent, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StatisticsDb>();

        db.Events.Add(new EventLogEntry
        {
            ServiceName = serviceEvent.ServiceName,
            Action = serviceEvent.Action,
            Username = serviceEvent.Username,
            Details = serviceEvent.Details,
            DurationMs = serviceEvent.DurationMs,
            CreatedAt = serviceEvent.Timestamp == default ? DateTime.UtcNow : serviceEvent.Timestamp
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
