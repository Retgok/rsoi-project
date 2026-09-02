namespace ApiGatewayService;

public class BonusService
{
    private readonly BonusClient _client;
    private readonly ICircuitBreaker _breaker;

    public BonusService(BonusClient client, ICircuitBreaker breaker)
    {
        _client = client;
        _breaker = breaker;
    }

    public async Task<PrivilegeInfoResponse?> GetPrivilegeCriticalAsync(string username)
    {
        return await _breaker.ExecuteAsync(
            action: () => _client.GetPrivilegeAsync(username),
            fallback: () => null,
            isCritical: true
        );
    }

    public async Task<PrivilegeShortInfo?> GetPrivilegeSafeAsync(string username)
    {
        var full = await _breaker.ExecuteAsync(
            action: () => _client.GetPrivilegeAsync(username),
            fallback: () => null,
            isCritical: false
        );

        if (full == null)
            return new PrivilegeShortInfo();

        return new PrivilegeShortInfo
        {
            Balance = full.Balance,
            Status = full.Status
        };
    }
}
