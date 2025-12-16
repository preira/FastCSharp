using FastCSharp.Exceptions;

namespace FastCSharp.Circuit.Breaker;

/// <summary>
/// Consecutive failures circuit breaker strategy opens a circuit after a given number of
/// registered consecutive execution failures.
/// The backoff duration is given by the backoff strategy.
/// If an uncontrolled failure happens it may immediately open the circuit.
/// </summary>
public class FailuresThresholdBreakerStrategy : BreakerStrategy
{
    private const string ExceptionMessage = "This Strategy has no associated Circuit.";
    readonly long threshold;
    readonly IBackoffStrategy backoff;
    int counter;
    readonly bool isOpenImmediately;

    /// <summary>
    /// Implements a strategy for Breaking a circuit based on consecutive failures after which the circuit will 
    /// be open for the duration given by the backoff strategy.
    /// If an uncontrolled failure happens it may immediately open the circuit.
    /// </summary>
    /// <param name="failureThreshold">The threshold for the number of failures.</param>
    /// <param name="backoffStrategy">The backoff strategy to calculate the backoff duration.</param>
    /// <param name="isShouldOpenImmediately">If set to true, a distinction will be made between uncontrolled exceptions and CircuitExceptions.
    /// The default is false.</param>
    public FailuresThresholdBreakerStrategy(long failureThreshold, IBackoffStrategy backoffStrategy, bool isShouldOpenImmediately = false)
    {
        threshold = failureThreshold;
        backoff = backoffStrategy;
        isOpenImmediately = isShouldOpenImmediately;
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

        if (Interlocked.Increment(ref counter) > threshold)
        {
            Breaker.Open(backoff.Duration);
        }
    }

    public override void RegisterUncontrolledFailure()
    {
        if (isOpenImmediately)
        {
            if (Breaker == null)
                throw new IncorrectInitializationException(ExceptionMessage);

            Interlocked.Increment(ref counter);
            Breaker.Open(backoff.Duration);
        }
        else
        {
            RegisterFailure();
        }
    }

    private void ResetCounter()
    {
        Interlocked.Exchange(ref counter, 0);
    }

}

