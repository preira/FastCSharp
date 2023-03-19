namespace FastCSharp.CircuitBreaker;

enum CircuitStatus
{
    OPEN,
    CLOSED,
    HALF_CLOSED,
}

public abstract class AbstractBreaker : Breaker
{
    CircuitStatus Status { get; set; }
    protected DateTime lastOpenTimestamp;
    protected DateTime closeTimestamp;

    protected AbstractBreaker(BreakerStrategy strategy) : base(strategy)
    {
        Close();
    }

    public override void Open(TimeSpan duration)
    {
        lastOpenTimestamp = DateTime.Now;
        closeTimestamp = lastOpenTimestamp + duration;
        Status = CircuitStatus.OPEN;
    }

    public override void Close() => Status = CircuitStatus.CLOSED;
    public override void Closing() => Status = CircuitStatus.HALF_CLOSED;
    public bool IsOpen() => IsStillOpen() && Status == CircuitStatus.OPEN;
    public bool IsClosed() => !IsStillOpen() && Status == CircuitStatus.CLOSED;
    public bool IsHalfclosed() => Status == CircuitStatus.HALF_CLOSED;

    bool IsStillOpen()
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
        if (IsOpen())
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
}

/// <summary>
/// The BlockingCircuitBreaker blocks the execution for the duration of the backoff. 
/// Uncontrolled Exceptions may promote immediate opening of the circuit 
/// depending on the BreakerStrategy
/// </summary>
public class BlockingCircuitBreaker : AbstractBreaker
{
    public BlockingCircuitBreaker(BreakerStrategy strategy) : base(strategy)
    {
    }

    public override TResult Wrap<TResult>(Func<TResult> callback)
    {
        if (IsOpen())
        {
            // Since Sleep truncates the interval value at milliseconds, we need to round up
            // to make sure the elapse time is greater than the remaing interval.
            // Otherwise it will interfere with tests.
            var interval = (closeTimestamp - DateTime.Now).TotalMilliseconds;
            var millisecondsTimeout = (int)Math.Round(interval, MidpointRounding.AwayFromZero);
            // TODO: consider using Task.Delay with a cancelation token that can be used via interface.
            // another difference is that the thread will be able to pickup another message... needs testing
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
}