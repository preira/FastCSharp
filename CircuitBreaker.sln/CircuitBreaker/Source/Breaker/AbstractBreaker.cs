using FastCSharp.Observability;

namespace FastCSharp.Circuit.Breaker;

public abstract class AbstractBreaker : Breaker, IHealthReporter
{
    protected CircuitStatus Status { get; set; }
    protected DateTime lastOpenTimestamp;
    protected DateTime closeTimestamp;
    protected SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
    protected AbstractBreaker(BreakerStrategy strategy) : base(strategy)
    {
        Status = CircuitStatus.CLOSED;
    }

    public override bool Open(TimeSpan duration)
    {
        return WithLock(() =>
        {
            var previousStatus = Status;
            lastOpenTimestamp = DateTime.Now;
            closeTimestamp = lastOpenTimestamp + duration;
            Status = CircuitStatus.OPEN;
            return previousStatus != Status;
        });
    }
    public override bool Close() 
    {
        return WithLock(() =>
        {
            var previousStatus = Status;
            Status = CircuitStatus.CLOSED;
            return previousStatus != Status;
        });
    } 

    public override bool Closing()
    {
        var previousStatus = Status;
        Status = CircuitStatus.HALF_CLOSED;
        return previousStatus != Status;
    } 

    public bool IsOpen => IsItStillOpen() && Status == CircuitStatus.OPEN;
    public bool IsClosed => !IsItStillOpen() && Status == CircuitStatus.CLOSED;
    public bool IsHalfclosed => Status == CircuitStatus.HALF_CLOSED;

    bool IsItStillOpen()
    {
        if (Status == CircuitStatus.OPEN && DateTime.Now > closeTimestamp)
        {
            // closing will allow for one attempt to be performed.
            Closing();
            return false;
        }
        return Status == CircuitStatus.OPEN;
    }

    /// <summary>
    /// Wraps the callback with this circuit breaker.
    /// </summary>
    /// <param name="callback">The function to be called when circuit is closed.</param>
    /// <typeparam name="TResult">The return type for the given callback function</typeparam>
    /// <returns>The callback return, or throws either any Exception that comes out 
    /// of the callback call or a OpenCircuitException if the circuit is open.</returns>
    public abstract Func<TResult> Wrap<TResult>(Func<TResult> callback);

    public abstract Func<Task<TResult>> WrapAsync<TResult>(Func<Task<TResult>> callback);

    public abstract Func<TInput, Task<TResult>> WrapAsync<TResult,TInput>(Func<TInput, Task<TResult>> callback);

    /// <summary>
    /// Simplified version of Wrap<TResult> for methods returning void.
    /// </summary>
    /// <param name="callback">The function to be called when circuit is closed.</param>
    public virtual Func<bool> Wrap(Action callback)
    {
        return Wrap(() =>
        {
            callback();
            return true;
        });
    }

    public async Task<IHealthReport> ReportHealthStatusAsync()
    {
        return await Task.Run(() => 
        {
            var status = IsOpen ? HealthStatus.Unhealthy : HealthStatus.Healthy;
            var report = new HealthReport(GetType().Name, status)
            {
                Description = $"Circuit is {(IsOpen ? "open" : "closed")}"
            };
            return report;
        });
    }

    protected T WithLock<T>(Func<T> action)
    {
        bool isLockAcquired = false;
        try
        {
            isLockAcquired = semaphoreSlim.Wait(TimeSpan.FromMilliseconds(100));
            return action();
        }
        finally
        {
            if (isLockAcquired)
            {
                semaphoreSlim.Release();
            }
        }
    }
}
