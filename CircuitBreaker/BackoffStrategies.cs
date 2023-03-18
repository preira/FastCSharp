
namespace FastCSharp.Circuit_Breaker;

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
            if (counter < maxIncrements)
            {
                return backoff + counter++ * increment;
            } 
            return backoff + counter * increment;
        }
        private set => backoff = value;
    }
    public void Reset() => counter = 0;
}

/// <summary>
/// Random backoff strategy provides a random duration value between the initial duration and 
/// the initial duration + maxIncrement.
/// </summary>
public class RandomBackoff : IBackoffStrategy
{
    TimeSpan backoff;
    readonly TimeSpan increment;
    Random sequence;

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
        sequence = new Random(DateTime.Now.Nanosecond);
    }
    public TimeSpan Duration
    {
        get => backoff + sequence.NextDouble() * increment;
        private set => backoff = value;
    }
    public void Reset() => sequence = new Random(DateTime.Now.Nanosecond);
}

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
    Random sequence;
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
        sequence = new Random(DateTime.Now.Nanosecond);
        counter = 0;
    }
    public TimeSpan Duration
    {
        get
        {
            ++counter;
            if (counter == 1)
            {
                return backoff;
            }
            else if (counter <  maxIncrements)
            {
                backoff += (sequence.NextDouble() * increments);
                return backoff;
            }
            else
            {
                return backoff + sequence.NextDouble() * increments;
            }
        }

        private set => backoff = value;
    }
    public void Reset()
    {
        sequence = new Random(DateTime.Now.Nanosecond);
        backoff = initialBackoff;
        counter = 0;
    } 
}
