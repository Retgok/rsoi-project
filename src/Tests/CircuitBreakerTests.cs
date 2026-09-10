using ApiGatewayService;

namespace Tests;

public class CircuitBreakerTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsResult_WhenActionSucceeds()
    {
        var breaker = new CircuitBreaker(failureThreshold: 3, openTimeout: TimeSpan.FromSeconds(5));

        var result = await breaker.ExecuteAsync(
            "svc",
            () => Task.FromResult<string?>("ok"),
            () => "fallback",
            isCritical: false);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFallback_WhenActionFailsAndNotCritical()
    {
        var breaker = new CircuitBreaker(failureThreshold: 3, openTimeout: TimeSpan.FromSeconds(5));

        var result = await breaker.ExecuteAsync<string>(
            "svc",
            () => throw new InvalidOperationException("down"),
            () => "fallback",
            isCritical: false);

        Assert.Equal("fallback", result);
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenActionFailsAndCritical()
    {
        var breaker = new CircuitBreaker(failureThreshold: 3, openTimeout: TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            breaker.ExecuteAsync<string>(
                "svc",
                () => throw new InvalidOperationException("down"),
                () => "fallback",
                isCritical: true));
    }

    [Fact]
    public async Task ExecuteAsync_OpensCircuit_AfterFailureThreshold()
    {
        var breaker = new CircuitBreaker(failureThreshold: 2, openTimeout: TimeSpan.FromSeconds(30));
        var calls = 0;

        Task<string?> FailingAction()
        {
            calls++;
            throw new Exception("fail");
        }

        await breaker.ExecuteAsync("svc", FailingAction, () => "fb", isCritical: false);
        await breaker.ExecuteAsync("svc", FailingAction, () => "fb", isCritical: false);

        var third = await breaker.ExecuteAsync(
            "svc",
            () => { calls++; return Task.FromResult<string?>("should-not-run"); },
            () => "fallback-open",
            isCritical: false);

        Assert.Equal("fallback-open", third);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForCritical_WhenCircuitOpen()
    {
        var breaker = new CircuitBreaker(failureThreshold: 1, openTimeout: TimeSpan.FromSeconds(30));

        await breaker.ExecuteAsync<string>(
            "svc",
            () => throw new Exception("fail"),
            () => "fb",
            isCritical: false);

        await Assert.ThrowsAsync<Exception>(() =>
            breaker.ExecuteAsync<string>(
                "svc",
                () => Task.FromResult<string?>("x"),
                () => "fb",
                isCritical: true));
    }

    [Fact]
    public async Task ExecuteAsync_IsolatesDependencies()
    {
        var breaker = new CircuitBreaker(failureThreshold: 1, openTimeout: TimeSpan.FromSeconds(30));

        await breaker.ExecuteAsync<string>(
            "flights",
            () => throw new Exception("flight down"),
            () => "fb",
            isCritical: false);

        var tickets = await breaker.ExecuteAsync(
            "tickets",
            () => Task.FromResult<string?>("tickets-ok"),
            () => "fb",
            isCritical: true);

        Assert.Equal("tickets-ok", tickets);
    }
}
