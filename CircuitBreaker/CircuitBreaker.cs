namespace FastCSharp.CircuitBreaker;

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
