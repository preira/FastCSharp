using System.Runtime.Serialization;
using FastCSharp.Observability;

namespace FastCSharp.CircuitBreaker;

[Serializable]
public class CircuitException : Exception
{
    public CircuitException() : base()
    {
        // intentionally empty
    }
    public CircuitException(string? message) : base(message)
    {
        // intentionally empty
    }
    protected CircuitException(SerializationInfo info, StreamingContext context) 
    : base(info, context)
    {
        // intentionally empty
    }
    public CircuitException(string? message, Exception? inner) : base(message, inner)
    {
        // intentionally empty
    }
}


[Serializable]
public class OpenCircuitException: Exception
{
    public OpenCircuitException() : base()
    {
        // intentionally empty
    }
    public OpenCircuitException(string? message) : base(message)
    {
        // intentionally empty
    }
    protected OpenCircuitException(SerializationInfo info, StreamingContext context) 
    : base(info, context)
    {
        // intentionally empty
    }
    public OpenCircuitException(string? message, Exception? inner) : base(message, inner)
    {
        // intentionally empty
    }
}


public enum CircuitStatus
{
    OPEN,
    CLOSED,
    HALF_CLOSED,
}

public abstract class AbstractBreaker : Breaker, IHealthReporter
{
    protected CircuitStatus Status { get; set; }
    protected DateTime lastOpenTimestamp;
    protected DateTime closeTimestamp;
    protected AbstractBreaker(BreakerStrategy strategy) : base(strategy)
    {
        Close();
    }

    public override bool Open(TimeSpan duration)
    {
        var previousStatus = Status;
        lastOpenTimestamp = DateTime.Now;
        closeTimestamp = lastOpenTimestamp + duration;
        Status = CircuitStatus.OPEN;
        return previousStatus != Status;
    }
    public override bool Close() 
    {
        var previousStatus = Status;
        Status = CircuitStatus.CLOSED;
        return previousStatus != Status;
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
    public abstract TResult Wrap<TResult>(Func<TResult> callback);

    public abstract Task<TResult> WrapAsync<TResult>(Func<Task<TResult>> callback);

    public abstract Func<TInput, Task<TResult>> WrapAsync<TResult, TInput>(Func<TInput, Task<TResult>> callback);

    /// <summary>
    /// Simplified version of Wrap<TResult> for methods returning void.
    /// </summary>
    /// <param name="callback">The function to be called when circuit is closed.</param>
    public virtual void Wrap(Action callback)
    {
        Wrap<Boolean>(() =>
        {
            callback();
            return true;
        });
    }

    public async Task<IHealthReport> ReportHealthStatus()
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
}

/// <summary>
/// The circuit breaker creates an open circuit by not executing the callback
/// and throws a OpenCircuitException if the circuit is open.
/// Uncontrolled Exceptions may promote immediate opening of the circuit 
/// depending on the BreakerStrategy
/// </summary>
public class CircuitBreaker : AbstractBreaker
{
    public CircuitBreaker(BreakerStrategy strategy) : base(strategy)
    {
    }

    public override TResult Wrap<TResult>(Func<TResult> callback)
    {
        if (IsOpen)
        {
            throw new OpenCircuitException();
        }
        else /* either closing or closed */
        {
            try
            {
                var result = callback();
                Strategy.RegisterSucess();
                return result;
            }
            catch (Exception e)
            {
                if (e is CircuitException)
                {
                    Strategy.RegisterFailure();
                }
                else
                {
                    Strategy.RegisterUncontrolledFailure();
                }
                throw;
            }
        }
    }
    public override async Task<TResult> WrapAsync<TResult>(Func<Task<TResult>> callback)
    {
        if (IsOpen)
        {
            throw new OpenCircuitException();
        }
        else /* either closing or closed */
        {
            try
            {
                var result = await callback();
                Strategy.RegisterSucess();
                return result;
            }
            catch (Exception e)
            {
                if (e is CircuitException)
                {
                    Strategy.RegisterFailure();
                }
                else
                {
                    Strategy.RegisterUncontrolledFailure();
                }
                throw;
            }
        }
    }

    public override Func<TInput, Task<TResult>> WrapAsync<TResult, TInput>(Func<TInput, Task<TResult>> callback)
    {
        return async (TInput input) =>
        {
            if (IsOpen)
            {
                throw new OpenCircuitException();
            }
            else /* either closing or closed */
            {
                try
                {
                    var result = await callback(input);
                    Strategy.RegisterSucess();
                    return result;
                }
                catch (Exception e)
                {
                    if (e is CircuitException)
                    {
                        Strategy.RegisterFailure();
                    }
                    else
                    {
                        Strategy.RegisterUncontrolledFailure();
                    }
                    throw;
                }
            }
        };
    }
}

/// <summary>
/// The BlockingCircuitBreaker blocks the execution for the duration of the backoff. 
/// Uncontrolled Exceptions may promote immediate opening of the circuit 
/// depending on the BreakerStrategy
/// </summary>
public class BlockingCircuitBreaker : AbstractBreaker
{
    public const string TypeName = "BlockingCircuitBreaker";
    public BlockingCircuitBreaker(BreakerStrategy strategy) : base(strategy)
    {
    }

    public override TResult Wrap<TResult>(Func<TResult> callback)
    {
        if (IsOpen)
        {
            // Since Sleep truncates the interval value at milliseconds, we need to round up
            // to make sure the elapse time is greater than the remaing interval.
            // Otherwise it will interfere with tests.
            var interval = (closeTimestamp - DateTime.Now).TotalMilliseconds;
            var millisecondsTimeout = (int)Math.Round(interval, MidpointRounding.AwayFromZero);
            Thread.Sleep(millisecondsTimeout);
            throw new OpenCircuitException();
        }
        else /* either closing or closed */
        {
            try
            {
                var result = callback();
                Strategy.RegisterSucess();
                return result;
            }
            catch (Exception e)
            {
                if (e is CircuitException)
                {
                    Strategy.RegisterFailure();
                }
                else
                {
                    Strategy.RegisterUncontrolledFailure();
                }
                throw;
            }
        }
    }

    public override async Task<TResult> WrapAsync<TResult>(Func<Task<TResult>> callback)
    {
        if (IsOpen)
        {
            // Since Sleep truncates the interval value at milliseconds, we need to round up
            // to make sure the elapse time is greater than the remaing interval.
            // Otherwise it will interfere with tests.
            var interval = (closeTimestamp - DateTime.Now).TotalMilliseconds;
            var millisecondsTimeout = (int)Math.Round(interval, MidpointRounding.AwayFromZero);
            Thread.Sleep(millisecondsTimeout);
            throw new OpenCircuitException();
        }
        else /* either closing or closed */
        {
            try
            {
                var result = await callback();
                Strategy.RegisterSucess();
                return result;
            }
            catch (Exception e)
            {
                if (e is CircuitException)
                {
                    Strategy.RegisterFailure();
                }
                else
                {
                    Strategy.RegisterUncontrolledFailure();
                }
                throw;
            }
        }
    }
    
    public override Func<TInput, Task<TResult>> WrapAsync<TResult, TInput>(Func<TInput, Task<TResult>> callback)
    {
        return async (TInput input) =>
        {
            if (IsOpen)
            {
                // Since Sleep truncates the interval value at milliseconds, we need to round up
                // to make sure the elapse time is greater than the remaing interval.
                // Otherwise it will interfere with tests.
                var interval = (closeTimestamp - DateTime.Now).TotalMilliseconds;
                var millisecondsTimeout = (int)Math.Round(interval, MidpointRounding.AwayFromZero);
                Thread.Sleep(millisecondsTimeout);
                throw new OpenCircuitException();
            }
            else /* either closing or closed */
            {
                try
                {
                    var result = await callback(input);
                    Strategy.RegisterSucess();
                    return result;
                }
                catch (Exception e)
                {
                    if (e is CircuitException)
                    {
                        Strategy.RegisterFailure();
                    }
                    else
                    {
                        Strategy.RegisterUncontrolledFailure();
                    }
                    throw;
                }
            }
        };
    }
}

// create a circuit breaker that is Event driven, based on the AbstractBreaker class. 
// It should be able to be used as a decorator for any method that returns a value or void.
public class EventDrivenCircuitBreaker : CircuitBreaker
{
    public const string TypeName = "EventDrivenCircuitBreaker";
    private TimeSpan _duration;
    private CancellationTokenSource? cancellationTokenSource;

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
            await Task.Delay(millisecondsDelay, cancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            // backoff was cancelled
        }

        Closing();
    }

}
