using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SharedEvents;

public sealed record ServiceEvent(
    string ServiceName,
    string Action,
    string? Username,
    string? Details,
    DateTime Timestamp,
    int? DurationMs = null
);

public interface IEventPublisher
{
    void Publish(ServiceEvent serviceEvent);
}

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly string _topic;
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IConfiguration configuration, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        _topic = configuration["Kafka:Topic"] ?? "service-events";
        var bootstrap = configuration["Kafka:BootstrapServers"] ?? "kafka:9092";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrap,
            Acks = Acks.All
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public void Publish(ServiceEvent serviceEvent)
    {
        try
        {
            var payload = JsonSerializer.Serialize(serviceEvent);
            _producer.Produce(_topic, new Message<string, string>
            {
                Key = serviceEvent.ServiceName,
                Value = payload
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish event {Action}", serviceEvent.Action);
        }
    }

    public void Dispose() => _producer.Dispose();
}

public static class EventPublisherExtensions
{
    public static IServiceCollection AddKafkaEventPublisher(this IServiceCollection services)
    {
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        return services;
    }
}
