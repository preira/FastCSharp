using FastCSharp.Criptography;

namespace FastCSharp.CircuitBreaker;

/// <summary>
/// Random backoff strategy provides a random duration value between the initial duration and 
/// the initial duration + maxIncrement.
/// </summary>
public class RandomBackoff : IBackoffStrategy
{
    TimeSpan backoff;
    readonly TimeSpan increment;
    readonly int precision = 5;

    /// <summary>
    /// Random backoff strategy provides a random duration value between the initial duration and 
    /// the initial duration + maxIncrement.
    /// </summary>
    /// <param name="duration">A TimeSpan object representing the backoff duration in milliseconds.</param>
    /// <param name="maxIncrement">A TimeSpan object representing the maximum increment to be added to the backoff duration.</param>
    public RandomBackoff(TimeSpan duration, TimeSpan maxIncrement)
    {
        backoff = duration;
        increment = maxIncrement;
    }
    public TimeSpan Duration
    {
        get => backoff + Rnd.GetRandomDouble(precision) * increment;
    }
    public void Reset() { /* intentionally empty. Nothing to do here. */ }
}
