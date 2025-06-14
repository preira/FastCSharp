namespace FastCSharp.Circuit.Breaker;

// create a circuit breaker that is Event driven, based on the AbstractBreaker class. 
// It should be able to be used as a decorator for any method that returns a value or void.
public class EventDrivenCircuitBreaker : CircuitBreaker
{
    public const string TypeName = "EventDrivenCircuitBreaker";
    private TimeSpan _duration;
    private volatile CancellationTokenSource? cancellationTokenSource;
    

    private event Action<object>? OnResetListenners;
    public event Action<object> OnReset
    {
        add { OnResetListenners += value; }
        remove { OnResetListenners -= value; }
    }

    private event Action<object>? OnOpenListenners;
    public event Action<object> OnOpen
    {
        add { OnOpenListenners += value; }
        remove { OnOpenListenners -= value; }
    }

    public EventDrivenCircuitBreaker(BreakerStrategy strategy) : base(strategy)
    {
    }

    public override bool Open(TimeSpan duration)
    {
        _duration = duration;
        if (base.Open(duration))
        {
            OnOpenListenners?.Invoke(this);
            #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            TryToRecoverWithDelay();
            #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            return true;
        }
        return false;
    }

    public override bool Closing()
    {
        var didItChange = base.Closing();
        if (didItChange)
        {
            OnResetListenners?.Invoke(this);
        }
        return didItChange;
    }

    /// <summary>
    /// Cancel the backoff and starts the closing of the circuit.
    /// </summary>
    /// <returns></returns>
    public bool CancelBackoff()
    {
        if (Status != CircuitStatus.CLOSED)
        {
            closeTimestamp = DateTime.Now;
            cancellationTokenSource?.Cancel();
            return true;
        }
        return false;
    }

    private async Task TryToRecoverWithDelay()
    {
        bool hasLock = await semaphoreSlim.WaitAsync(TimeSpan.FromMilliseconds(100));
        try
        {
            // Someone else may have closed the circuit while we were waiting for the lock.
            if (Status != CircuitStatus.OPEN)
            {
                return;
            }
            var now = DateTime.Now;
            if (closeTimestamp.CompareTo(now) < 0)
            {
                // reset backoff interval
                closeTimestamp = lastOpenTimestamp + _duration;
            }
            var interval = (closeTimestamp - now).TotalMilliseconds;
            interval = Math.Max(0, interval);
            var millisecondsDelay = (int)Math.Round(interval, MidpointRounding.AwayFromZero);

            cancellationTokenSource = new CancellationTokenSource();
            try
            {
                semaphoreSlim.Release();
                await Task.Delay(millisecondsDelay, cancellationTokenSource.Token);

                await semaphoreSlim.WaitAsync(TimeSpan.FromMilliseconds(100));
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
            catch (TaskCanceledException)
            {
                // backoff was cancelled
            }

            Closing();
        }
        finally
        {
            if (hasLock)
            {
                semaphoreSlim.Release();
            }
        }
    }

}
