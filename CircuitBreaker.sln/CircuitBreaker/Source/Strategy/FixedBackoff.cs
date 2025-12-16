namespace FastCSharp.Circuit.Breaker;

/// <summary>
/// Fixed backoff strategy simply stores a fixed duration.
/// </summary>
public class FixedBackoff : IBackoffStrategy
{
    TimeSpan backoff;
    public TimeSpan Duration { get => backoff; private set => backoff = value; }
    
    /// <summary>
    /// Fixed backoff strategy simply stores a fixed duration.
    /// </summary>
    /// <param name="duration">A TimeSpan object representing the backoff duration in milliseconds.</param>
    public FixedBackoff(TimeSpan duration)
    {
        Duration = duration;
    }
    public void Reset() { /* no action to perform for fixed backoff */ }
}
