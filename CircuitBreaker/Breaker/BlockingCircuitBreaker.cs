namespace FastCSharp.Circuit.Breaker;

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

    public override Func<TResult> Wrap<TResult>(Func<TResult> callback)
    {
        return () =>
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
        };
    }

    public override Func<Task<TResult>> WrapAsync<TResult>(Func<Task<TResult>> callback)
    {
        return async () =>
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
        };
    }
    
    public override Func<TInput, Task<TResult>> WrapAsync<TResult, TInput>(Func<TInput, Task<TResult>> callback)
    {
        return async (input) =>
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
