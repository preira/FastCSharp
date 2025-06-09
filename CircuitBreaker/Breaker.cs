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

