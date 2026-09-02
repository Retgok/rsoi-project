using System;
using System.Threading;
using System.Threading.Tasks;

namespace ApiGatewayService;

public interface ICircuitBreaker
{
    Task<T?> ExecuteAsync<T>(
        Func<Task<T?>> action,
        Func<T?> fallback,
        bool isCritical
    );
}

public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openTimeout;

    private int _failures;
    private DateTime _openedAt;
    private CircuitState _state = CircuitState.Closed;

    private readonly SemaphoreSlim _lock = new(1, 1);

    public CircuitBreaker(int failureThreshold, TimeSpan openTimeout)
    {
        _failureThreshold = failureThreshold;
        _openTimeout = openTimeout;
    }

    public async Task<T?> ExecuteAsync<T>(
        Func<Task<T?>> action,
        Func<T?> fallback,
        bool isCritical)
    {
        if (_state == CircuitState.Open)
        {
            if (DateTime.UtcNow - _openedAt < _openTimeout)
            {
                if (isCritical)
                    throw new Exception("Critical service unavailable");

                return fallback();
            }

            // half-open
            _state = CircuitState.HalfOpen;
        }

        try
        {
            var result = await action();

            await ResetAsync();
            return result;
        }
        catch
        {
            await RegisterFailureAsync();

            if (isCritical)
                throw;

            return fallback();
        }
    }

    private async Task RegisterFailureAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _failures++;

            if (_failures >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ResetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _failures = 0;
            _state = CircuitState.Closed;
        }
        finally
        {
            _lock.Release();
        }
    }

    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }
}
