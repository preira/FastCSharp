namespace FastCSharp.CircuitBreaker;

/// <summary>
/// Circuit Breaker Configuration.
/// </summary>
public class CircuitBreakerConfig
{
    public const string SectionName = "CircuitBreaker";
    /// <summary>
    /// The type of circuit breaker to use. 
    /// </summary>
    /// <value></value>
    public string? Type { get; set; }

    /// <summary>
    /// The Breaker Strategy to be used to open the circuit.
    /// </summary>
    public BreakerStrategyConfig? BreakerStrategy { get; set; }

    /// <summary>
    /// /// The Backoff Strategy to be used on open circuit.
    /// </summary>
    /// <value></value>
    public BackoffStrategyConfig? BackoffStrategy { get; set; }
}

/// <summary>
/// Breaker Strategy Configuration.
/// </summary>
public class BreakerStrategyConfig
{
    public const string SectionName = "Strategy";
    /// <summary>
    /// The type of breaker strategy to use.
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// Breaker Strategy Configuration.
/// </summary>
public class FailureThresholdStrategyConfig : BreakerStrategyConfig
{
    public new const string SectionName = "FailureThresholdStrategy";
    /// <summary>
    /// The number of failures before opening the circuit.
    /// </summary>
    public int? Threshold { get; set; }
}

/// <summary>
/// Backoff Strategy Configuration.
/// </summary>
public class BackoffStrategyConfig
{
    public const string SectionName = "Backoff";
    /// <summary>
    /// The type of backoff strategy to use.
    /// </summary>
    public string? Type { get; set; }
}

public class FixedBackoffConfig : BackoffStrategyConfig
{
    public new const string SectionName = "FixedBackoff";
    /// <summary>
    /// The duration of the backoff.
    /// </summary>
    public TimeSpan? Duration { get; set; }
}

public class IncrementalBackoffConfig : BackoffStrategyConfig
{
    public new const string SectionName = "IncrementalBackoff";
    /// <summary>
    /// The duration of the initial backoff.
    /// </summary>
    public TimeSpan? MinBackoff { get; set; }

    /// <summary>
    /// The increment of the backoff.
    /// </summary>
    /// <value></value>
    /// <remarks>
    /// The increment is added to the duration for each request.
    /// </remarks>
    public TimeSpan? Increment { get; set; }


    /// <summary>
    /// The maximum number of increments to be added to the duration.
    /// </summary>
    /// <value></value>
    /// <remarks>
    /// The maximum number of increments to be added to the duration.
    /// </remarks>
    public long? MaxIncrements { get; set; }
}

