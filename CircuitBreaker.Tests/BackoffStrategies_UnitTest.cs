using Xunit;
using FastCSharp.SDK.Circuit_Breaker;

namespace Circuit_Breaker.Tests;

public class FixedBackoff_Tests
{
    [Fact]
    public void CreateStrategy()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        FixedBackoff backoff = new FixedBackoff(duration);
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
    }

    [Fact]
    public void ImmutableDuration()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        FixedBackoff backoff = new FixedBackoff(duration);
        duration += duration * 2;
        Assert.NotEqual<TimeSpan>(duration, backoff.Duration);
    }

    [Fact]
    public void ResetBackoff()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        FixedBackoff backoff = new FixedBackoff(duration);
        backoff.Reset();
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
    }
}

public class IncrementalBackoff_Tests
{
    [Fact]
    public void CreateStrategy()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        IncrementalBackoff backoff = new IncrementalBackoff(duration, increment);
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
    }

    [Fact]
    public void NextDurationsAreIncrements()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        IncrementalBackoff backoff = new IncrementalBackoff(duration, increment);
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
        for (var i = 0; i < 12; ++i)
        {
            Assert.NotEqual<TimeSpan>(duration, backoff.Duration);
        }
    }

    [Fact]
    public void NextDurationsAreIncrementsMultiples()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 3);
        IncrementalBackoff backoff = new IncrementalBackoff(duration, increment);
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
        var i = 1;
        for (; i < 12; ++i)
        {
            Assert.NotEqual<TimeSpan>(duration, backoff.Duration);
        }
        TimeSpan totalIncrement = backoff.Duration - duration;
        Assert.Equal<int>(i, (int)(totalIncrement / increment));
    }

    [Fact]
    public void ResetBackoff()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        IncrementalBackoff backoff = new IncrementalBackoff(duration, increment);
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
        Assert.NotEqual<TimeSpan>(duration, backoff.Duration);
        backoff.Reset();
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
    }
}

public class RandomBackoff_Tests
{
    [Fact]
    public void BetweenMinAndMax()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 50);
        RandomBackoff backoff = new RandomBackoff(duration, increment);
        TimeSpan total = new TimeSpan();
        TimeSpan total2 = new TimeSpan();

        int i = 0;
        for (; i < 1011; ++i)
        {
            TimeSpan current = backoff.Duration;
            total += backoff.Duration;
            total2 += duration;
            Assert.True(current <= duration + increment, $"{current} <= {duration + increment}");
            Assert.True(current >= duration, $"{current} >= {duration}");
        }
        Assert.Equal<TimeSpan>(total2, duration * i);
        Assert.NotEqual<TimeSpan>(total, duration * i);
        Assert.NotEqual<TimeSpan>(total, (duration + increment) * i);
        Assert.True(total <= (duration + increment) * i, $"Total: {total} <= ({duration + increment}) * {i}");
        Assert.True(total >= duration * i, $"Total: {total} >= {duration} * {i}");
    }
}