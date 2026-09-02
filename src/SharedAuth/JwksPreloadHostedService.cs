using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedAuth;

internal sealed class JwksPreloadHostedService : IHostedService
{
    private readonly IJwksProvider _jwksProvider;
    private readonly ILogger<JwksPreloadHostedService> _logger;

    public JwksPreloadHostedService(
        IJwksProvider jwksProvider,
        ILogger<JwksPreloadHostedService> logger)
    {
        _jwksProvider = jwksProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            for (var attempt = 1; attempt <= 10 && !cancellationToken.IsCancellationRequested; attempt++)
            {
                var keys = _jwksProvider.GetSigningKeys().ToList();
                if (keys.Count > 0)
                    return;

                _logger.LogWarning(
                    "JWKS preload attempt {Attempt} failed, retrying in 3s",
                    attempt);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
