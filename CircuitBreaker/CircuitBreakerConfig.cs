namespace FastCSharp.CircuitBreaker;

/// <summary>
/// Circuit Breaker Configuration.
/// </summary>
public class CircuitBreakerConfig
{
    /// <summary>
    /// The type of circuit breaker to use. 
    /// </summary>
    /// <value></value>
    public string? Type { get; set; }

    /// <summary>
    /// The Backoff Strategy to be used on open circuit.
    /// </summary>
    /// <value></value>
    public BackoffStrategyConfig? BackoffStrategy { get; set; }
}

/// <summary>
/// Backoff Strategy Configuration.
/// </summary>
public class BackoffStrategyConfig
{
    /// <summary>
    /// The type of backoff strategy to use.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The duration of the backoff.
    /// </summary>
    public int? Duration { get; set; }
}

