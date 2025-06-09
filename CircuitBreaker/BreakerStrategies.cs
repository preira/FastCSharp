namespace FastCSharp.CircuitBreaker;

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

