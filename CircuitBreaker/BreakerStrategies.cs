namespace FastCSharp.CircuitBreaker;

/// <summary>
/// Circuit breaker implementations need to extend from this class in order to profit from BreakerStrategy implementations.
/// </summary>
public abstract class Breaker
{
    protected BreakerStrategy Strategy { get; private set; }
    abstract public bool Open(TimeSpan duration);
    abstract public bool Close();
    abstract public bool Closing();

    protected Breaker(BreakerStrategy strategy)
    {
        Strategy = strategy;
        Strategy.Breaker = this;
    }
}

/// <summary>
/// BreakerStrategy implements the strategy for a given circuit breaker.
/// </summary>
public abstract class BreakerStrategy
{
    internal Breaker? Breaker { set; get; }
    abstract public void RegisterFailure();
    abstract public void RegisterUncontrolledFailure();
    abstract public void RegisterSucess();
}

/// <summary>
/// Consecutive failures circuit breaker strategy opens a circuit after a given number of
/// registered consecutive execution failures.
/// The backoff duration is given by the backoff strategy.
/// If an uncontrolled failure happens it may immediately open the circuit.
/// </summary>
public class ConsecutiveFailuresBreakerStrategy : BreakerStrategy
{
    private const string ExceptionMessage = "This Strategy has no associated Circuit.";
    readonly long threshold;
    readonly IBackoffStrategy backoff;
    int counter;
    readonly bool isCloseImmediately;

    /// <summary>
    /// Implements a strategy for Breaking a circuit based on consecutive failures after which the circuit will 
    /// be open for the duration given by the backoff strategy.
    /// If an uncontrolled failure happens it may immediately open the circuit.
    /// </summary>
    /// <param name="failureThreshold">The threshold for the number of failures.</param>
    /// <param name="backoffStrategy">The backoff strategy to calculate the backoff duration.</param>
    /// <param name="isShouldImmediately">If set to true, a distinction will be made between uncontrolled exceptions and CircuitExceptions.
    /// The default is false.</param>
    public ConsecutiveFailuresBreakerStrategy(long failureThreshold, IBackoffStrategy backoffStrategy, Boolean isShouldImmediately = false)
    {
        threshold = failureThreshold;
        backoff = backoffStrategy;
        isCloseImmediately = isShouldImmediately;
        ResetCounter();
    }

    public override void RegisterSucess()
    {
        if (Breaker == null)
            throw new IncorrectInitializationException(ExceptionMessage);
        ResetCounter();
        Breaker.Close();
    }

    public override void RegisterFailure()
    {
        if (Breaker == null)
            throw new IncorrectInitializationException(ExceptionMessage);

        if (++counter > threshold)
        {
            Breaker.Open(backoff.Duration);
        }
    }

    public override void RegisterUncontrolledFailure()
    {
        if (isCloseImmediately)
        {
            if (Breaker == null)
                throw new IncorrectInitializationException(ExceptionMessage);

            ++counter;
            Breaker.Open(backoff.Duration);
        }
        else
        {
            RegisterFailure();
        }
    }

    private void ResetCounter()
    {
        counter = 0;
    }

}

