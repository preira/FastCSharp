namespace FastCSharp.CircuitBreaker;

/// <summary>
/// Incremental backoff strategy adds a time increment to subsequent Duration requests up to maxIncrements times.
/// The Reset() function resets the Duration back to its initial value. 
/// </summary>
public class IncrementalBackoff : IBackoffStrategy
{
    TimeSpan backoff;
    readonly TimeSpan increment;
    int counter;
    readonly long maxIncrements;

    /// <summary>
    /// Incremental backoff strategy adds a time increment to subsequent Duration requests up to maxIncrements times.
    /// The Reset() function resets the Duration back to its initial value. 
    /// </summary>
    /// <param name="duration">A TimeSpan object representing the backoff duration in milliseconds.</param>
    /// <param name="increments">A TimeSpan object representing the time incremented to the backoff for each duration resquested in milliseconds.</param>
    /// <param name="maxIncrements">Maximum number of increments to be added to the initial duration. Defaults to 100</param>
    public IncrementalBackoff(TimeSpan duration, TimeSpan increments, long maxIncrements = 100)
    {
        backoff = duration;
        increment = increments;
        this.maxIncrements = maxIncrements;
    }
    public TimeSpan Duration
    {
        get
        {
            var currentCounter = Volatile.Read(ref counter);
            if (currentCounter < maxIncrements)
            {
                return backoff + Interlocked.CompareExchange(ref counter, currentCounter + 1, currentCounter) * increment;
            } 
            return backoff + maxIncrements * increment;
        }
    }
    public void Reset() => Interlocked.Exchange(ref counter, 0);
}
