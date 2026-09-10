using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ApiGatewayService;

public interface ICircuitBreaker
{
    Task<T?> ExecuteAsync<T>(
        string dependency,
        Func<Task<T?>> action,
        Func<T?> fallback,
        bool isCritical
    );
}
public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openTimeout;
    private readonly ConcurrentDictionary<string, DependencyState> _states = new();

    public CircuitBreaker(int failureThreshold, TimeSpan openTimeout)
    {
        _failureThreshold = failureThreshold;
        _openTimeout = openTimeout;
    }

    public async Task<T?> ExecuteAsync<T>(
        string dependency,
        Func<Task<T?>> action,
        Func<T?> fallback,
        bool isCritical)
    {
        var state = _states.GetOrAdd(dependency, _ => new DependencyState());

        await state.Lock.WaitAsync();
        var skipCall = false;
        try
        {
            if (state.Circuit == CircuitState.Open)
            {
                if (DateTime.UtcNow - state.OpenedAt < _openTimeout)
                {
                    skipCall = true;
                }
                else
                {
                    state.Circuit = CircuitState.HalfOpen;
                }
            }
        }
        finally
        {
            state.Lock.Release();
        }

        if (skipCall)
        {
            if (isCritical)
                throw new Exception($"Critical service unavailable: {dependency}");

            return fallback();
        }

        try
        {
            var result = await action();
            await ResetAsync(state);
            return result;
        }
        catch
        {
            await RegisterFailureAsync(state);

            if (isCritical)
                throw;

            return fallback();
        }
    }

    private async Task RegisterFailureAsync(DependencyState state)
    {
        await state.Lock.WaitAsync();
        try
        {
            state.Failures++;
            if (state.Failures >= _failureThreshold)
            {
                state.Circuit = CircuitState.Open;
                state.OpenedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            state.Lock.Release();
        }
    }

    private async Task ResetAsync(DependencyState state)
    {
        await state.Lock.WaitAsync();
        try
        {
            state.Failures = 0;
            state.Circuit = CircuitState.Closed;
        }
        finally
        {
            state.Lock.Release();
        }
    }

    private sealed class DependencyState
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public int Failures;
        public DateTime OpenedAt;
        public CircuitState Circuit = CircuitState.Closed;
    }

    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }
}
