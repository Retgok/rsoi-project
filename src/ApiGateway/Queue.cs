using Microsoft.Extensions.Hosting;
using System.Threading.Channels;

namespace ApiGatewayService;

public record BonusRefundJob(
    string Username,
    Guid TicketUid,
    string? AuthorizationHeader
);

public enum RefundResult
{
    Success,
    NotNeeded,
    Retry
}

public interface IBonusRefundQueue
{
    ValueTask EnqueueAsync(BonusRefundJob job);
    ChannelReader<BonusRefundJob> Reader { get; }
}

public class BonusRefundQueue : IBonusRefundQueue
{
    private readonly Channel<BonusRefundJob> _channel = Channel.CreateUnbounded<BonusRefundJob>();

    public ValueTask EnqueueAsync(BonusRefundJob job) => _channel.Writer.WriteAsync(job);

    public ChannelReader<BonusRefundJob> Reader => _channel.Reader;
}

public class BonusRefundWorker : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly IBonusRefundQueue _queue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BonusRefundWorker> _logger;

    public BonusRefundWorker(
        IBonusRefundQueue queue,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<BonusRefundWorker> logger)
    {
        _queue = queue;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessJobAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessJobAsync(BonusRefundJob job, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await RefundAsync(job);

                if (result is RefundResult.Success or RefundResult.NotNeeded)
                {
                    _logger.LogInformation("Bonus refund completed for ticket {TicketUid}", job.TicketUid);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bonus refund failed for ticket {TicketUid}", job.TicketUid);
            }

            await Task.Delay(RetryDelay, stoppingToken);
        }
    }

    private async Task<RefundResult> RefundAsync(BonusRefundJob job)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_configuration["Services:BonusService"] ?? "http://bonus_service:8050");

        if (!string.IsNullOrWhiteSpace(job.AuthorizationHeader))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", job.AuthorizationHeader);

        var resp = await client.PostAsync($"/api/v1/privilege/refund/{job.TicketUid}", null);

        if (resp.IsSuccessStatusCode)
            return RefundResult.Success;

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return RefundResult.NotNeeded;

        return RefundResult.Retry;
    }
}
