using Xunit;
using FastCSharp.CircuitBreaker;

namespace Circuit_Breaker.Tests;

public class FixedBackoff_UnitTest
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

public class IncrementalBackoff_UnitTest
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

public class RandomBackoff_UnitTest
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

public class RandomIncrementalBackoff_UnitTest
{
    [Fact]
    public void RandomIncrementalBackoff_CreateStrategy()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        RandomIncrementalBackoff backoff = new RandomIncrementalBackoff(duration, increment);
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
    }

    [Fact]
    public void RandomIncrementalBackoff_NextDurationsAreIncrements()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        RandomIncrementalBackoff backoff = new RandomIncrementalBackoff(duration, increment);
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
        for (var i = 1; i < 12; ++i)
        {
            var current = backoff.Duration; // for debug
            Assert.True(duration + i * increment >= current, $"Increments should be inferior to increment value. Instead {duration + i * increment} => {current}, for i = {i}");
        }
    }

    [Fact]
    public void RandomIncrementalBackoff_NextDurationsAreStopIncrements()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        var maxIncrements = 3;
        RandomIncrementalBackoff backoff = new RandomIncrementalBackoff(duration, increment, maxIncrements);
        var previous = backoff.Duration;
        Assert.Equal<TimeSpan>(duration, previous);
        var current = previous; 
        for (var i = 1; i < maxIncrements; ++i)
        {
            Assert.True(duration + i * increment >= current, $"Increments should be inferior to increment value. Instead {duration + i * increment} => {current}, for i = {i}");
            Assert.True(current >= previous, $"Current Duration should be Greater than or Equal to the previous Duration: {current} >= {previous}; i={i}");
            previous = current;
            current = backoff.Duration; 
        }
        for (var i = maxIncrements; i < maxIncrements + 10; ++i)
        {
            Assert.True(duration + 10 * increment >= current, $"Increments should be inferior to increment value. Instead {duration + 2 * increment} => {current}, for last.");
            current = backoff.Duration;
        }
    }

    [Fact]
    public void RandomIncrementalBackoff_ResetDurationsIncrements()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        var maxIncrements = 10;
        RandomIncrementalBackoff backoff = new RandomIncrementalBackoff(duration, increment, maxIncrements);
        var previous = backoff.Duration;
        Assert.Equal<TimeSpan>(duration, previous);
        var current = previous; 
        for (var i = 1; i < maxIncrements; ++i)
        {
            Assert.True(duration + i * increment >= current, $"Increments should be inferior to increment value. Instead {duration + i * increment} => {current}, for i = {i}");
            Assert.True(current >= previous, $"Current Duration should be Greater than or Equal to the previous Duration: {current} >= {previous}; i={i}");
            previous = current;
            current = backoff.Duration; 
        }
        Assert.True(duration + increment <= current, $"Increments should be greater than initial increment value. Instead {duration + increment} <= {current}");
        backoff.Reset();
        current = backoff.Duration; 
        Assert.True(duration + increment >= current, $"After reset, increments should be inferior to increment value. Instead {duration + increment} >= {current}");

    }

    [Fact]
    public void IncrementalBackoff_NextDurationsAreStopIncrements()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        var maxIncrements = 3;
        IncrementalBackoff backoff = new IncrementalBackoff(duration, increment, maxIncrements);
        var previous = backoff.Duration;
        Assert.Equal<TimeSpan>(duration, previous);
        var current = previous; 
        for (var i = 1; i < maxIncrements; ++i)
        {
            Assert.True(duration + i * increment >= current, $"Increments should be inferior to increment value. Instead {duration + i * increment} => {current}, for i = {i}");
            Assert.True(current >= previous, $"Current Duration should be Greater than or Equal to the previous Duration: {current} >= {previous}; i={i}");
            previous = current;
            current = backoff.Duration; 
        }
        for (var i = maxIncrements; i < maxIncrements + 10; ++i)
        {
            Assert.True(duration + 10 * increment >= current, $"Increments should be inferior to increment value. Instead {duration + 2 * increment} => {current}, for last.");
            current = backoff.Duration;
        }
    }

    [Fact]
    public void IncrementalBackoff_ResetDurationsIncrements()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        var maxIncrements = 10;
        IncrementalBackoff backoff = new IncrementalBackoff(duration, increment, maxIncrements);
        var previous = backoff.Duration;
        Assert.Equal<TimeSpan>(duration, previous);
        var current = previous; 
        for (var i = 1; i < maxIncrements; ++i)
        {
            Assert.True(duration + i * increment >= current, $"Increments should be inferior to increment value. Instead {duration + i * increment} => {current}, for i = {i}");
            Assert.True(current >= previous, $"Current Duration should be Greater than or Equal to the previous Duration: {current} >= {previous}; i={i}");
            previous = current;
            current = backoff.Duration; 
        }
        Assert.True(duration + increment <= current, $"Increments should be greater than initial increment value. Instead {duration + increment} <= {current}");
        backoff.Reset();
        current = backoff.Duration; 
        Assert.True(duration + increment >= current, $"After reset, increments should be inferior to increment value. Instead {duration + increment} >= {current}");

    }

    [Fact]
    public void RandomIncrementalBackoff_NextDurationsAreIncrementsMultiples()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 3);
        RandomIncrementalBackoff backoff = new RandomIncrementalBackoff(duration, increment);
        TimeSpan previous = backoff.Duration;
        Assert.Equal<TimeSpan>(duration, previous);
        var i = 1;
        for (; i < 12; ++i)
        {
            var current = backoff.Duration;
            Assert.True(previous <= current, "Backoff Duration should monotonously augment.");
            previous = current;
        }
        TimeSpan totalIncrement = backoff.Duration - duration;
        // Assert.Equal<int>(i, (int)(totalIncrement / increment));
        Assert.True(i > (int)(totalIncrement / increment), $"Turns should be greater than the dividor. Instead {i} > {(int)(totalIncrement / increment)}");
    }

    [Fact]
    public void RandomIncrementalBackoff_ResetBackoff()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        RandomIncrementalBackoff backoff = new RandomIncrementalBackoff(duration, increment);
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
        Assert.NotEqual<TimeSpan>(duration, backoff.Duration);
        backoff.Reset();
        Assert.Equal<TimeSpan>(duration, backoff.Duration);
    }

    [Fact]
    public void RandomBackoff_ResetBackoff()
    {
        TimeSpan duration = new TimeSpan(0, 0, 5);
        TimeSpan increment = new TimeSpan(0, 0, 5);
        RandomBackoff backoff = new RandomBackoff(duration, increment);
        Assert.True(duration < backoff.Duration, $"Duration should add increment. duration < backoff.Duration : {duration} <= {backoff.Duration}");
        Assert.True(duration + increment > backoff.Duration, $"Duration should add increment. duration > backoff.Duration : {duration} >= {backoff.Duration}");
        backoff.Reset();
        Assert.True(duration < backoff.Duration, $"Duration should add increment. duration < backoff.Duration : {duration} <= {backoff.Duration}");
        Assert.True(duration + increment > backoff.Duration, $"Duration should add increment. duration > backoff.Duration : {duration} >= {backoff.Duration}");
    }
}

