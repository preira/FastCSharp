namespace FastCSharp.CircuitBreaker;

/// <summary>
/// Interface for different Backoff Strategies.
/// </summary>
public interface IBackoffStrategy
{
    /// <summary>
    /// Get the backoff duration according to the backoff strategy.
    /// </summary>
    /// <value>A TimeSpan object representing the backoff duration.</value>
    public TimeSpan Duration { get; }
    
    /// <summary>
    /// A way to reset the Strategy. It may not apply to all Strategies.
    /// </summary>
    void Reset();
}
