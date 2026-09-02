using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace SharedAuth;

public interface IJwksProvider
{
    IEnumerable<SecurityKey> GetSigningKeys();
}

public sealed class JwksProvider : IJwksProvider
{
    private readonly string _jwksUrl;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<JwksProvider>? _logger;
    private JsonWebKeySet? _cached;
    private DateTime _loadedAt = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public JwksProvider(string jwksUrl, ILogger<JwksProvider>? logger = null)
    {
        _jwksUrl = jwksUrl;
        _logger = logger;
    }

    public IEnumerable<SecurityKey> GetSigningKeys()
    {
        RefreshIfNeeded(force: _cached == null);
        return _cached?.GetSigningKeys() ?? Enumerable.Empty<SecurityKey>();
    }

    private void RefreshIfNeeded(bool force = false)
    {
        if (!force && _cached != null && DateTime.UtcNow - _loadedAt < CacheDuration)
            return;

        _lock.Wait();
        try
        {
            if (!force && _cached != null && DateTime.UtcNow - _loadedAt < CacheDuration)
                return;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = client.GetStringAsync(_jwksUrl).GetAwaiter().GetResult();
            _cached = new JsonWebKeySet(json);
            _loadedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load JWKS from {JwksUrl}", _jwksUrl);
        }
        finally
        {
            _lock.Release();
        }
    }
}
