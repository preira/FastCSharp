using FastCSharp.Cryptography;

namespace FastCSharp.CircuitBreaker;

/// <summary>
/// Implements a backoff strategy that continuously adds increments randomly generated between 0 and increments. 
/// </summary>
public class RandomIncrementalBackoff : IBackoffStrategy
{
    TimeSpan backoff;
    readonly TimeSpan initialBackoff;
    readonly TimeSpan increments;
    int counter;
    readonly long maxIncrements;
    readonly private int precision = 5;
    /// <summary>
    /// Implements a backoff strategy that continuously adds increments randomly generated between 0 and increments. 
    /// </summary>
    /// <param name="duration">A TimeSpan object representing the backoff duration in milliseconds.</param>
    /// <param name="increments">A TimeSpan object representing the time incremented to the backoff for each duration resquested in milliseconds which will be multiplied by a random factor.</param>
    /// <param name="maxIncrements">Maximum number of increments to be added to the initial duration. Defaults to 100</param>
    public RandomIncrementalBackoff(TimeSpan duration, TimeSpan increments, long maxIncrements = 100)
    {
        initialBackoff = duration;
        this.increments = increments;
        this.maxIncrements = maxIncrements;
        backoff = initialBackoff;
        counter = 0;
    }
    public TimeSpan Duration
    {
        get
        {
            Interlocked.Increment(ref counter);
            var currentCounter = Volatile.Read(ref counter);
            var currentBackoff = backoff;
            if (currentCounter == 1)
            {
                return currentBackoff;
            }
            else if (currentCounter < maxIncrements + 1)
            {
                currentBackoff += Rnd.GetRandomDouble(precision) * increments;
                backoff = currentBackoff;
                return currentBackoff;
            }
            else
            {
                return currentBackoff + Rnd.GetRandomDouble(precision) * increments;
            }
        }
    }
    public void Reset()
    {
        backoff = initialBackoff;
        counter = 0;
    } 
}
