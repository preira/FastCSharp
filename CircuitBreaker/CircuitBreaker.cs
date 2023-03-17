namespace FastCSharp.Circuit_Breaker;

enum CircuitStatus
{
    OPEN,
    CLOSED,
    HALF_CLOSED,
}

public class CircuitException : Exception
{
}

public class OpenCircuitException : Exception
{
}

public abstract class AbstractBreaker : Breaker
{
    CircuitStatus Status { get; set; }
    protected DateTime lastOpenTimestamp;
    protected DateTime lastCloseTimestamp;

    public AbstractBreaker(BreakerStrategy strategy) : base(strategy)
    {
        Close();
    }

    public override void Open(TimeSpan duration)
    {
        lastOpenTimestamp = DateTime.Now;
        lastCloseTimestamp = lastOpenTimestamp + duration;
        Status = CircuitStatus.OPEN;
    }

    public override void Close() => Status = CircuitStatus.CLOSED;
    public override void Closing() => Status = CircuitStatus.HALF_CLOSED;
    public bool IsOpen() => IsStillOpen() && Status == CircuitStatus.OPEN;
    public bool IsClosed() => !IsStillOpen() && Status == CircuitStatus.CLOSED;
    public bool IsHalfclosed() => Status == CircuitStatus.HALF_CLOSED;

    bool IsStillOpen()
    {
        if (Status == CircuitStatus.OPEN && DateTime.Now > lastCloseTimestamp)
        {
            // closing will allow for one attempt to be performed.
            Closing();
            return false;
        }
        return Status == CircuitStatus.OPEN;
    }

    public abstract TResult Wrap<TResult>(Func<TResult> callback);

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
            Thread.Sleep(lastCloseTimestamp - DateTime.Now);
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