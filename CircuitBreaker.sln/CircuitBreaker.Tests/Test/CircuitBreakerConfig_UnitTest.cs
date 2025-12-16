using Xunit;

namespace FastCSharp.Circuit.Breaker.Tests;

public class CircuitBreakerConfigTest
{
    [Fact]
    public void CircuitBreakerConfig_DefaultValues_ShouldBeNull()
    {
        var config = new CircuitBreakerConfig();
        Assert.Null(config.Type);
        Assert.Null(config.BreakerStrategy);
        Assert.Null(config.BackoffStrategy);
    }

    [Fact]
    public void CircuitBreakerConfig_SetProperties_ShouldAssignValues()
    {
        var breakerStrategy = new BreakerStrategyConfig { Type = "FailureThreshold" };
        var backoffStrategy = new BackoffStrategyConfig { Type = "FixedBackoff" };
        var config = new CircuitBreakerConfig
        {
            Type = "CustomType",
            BreakerStrategy = breakerStrategy,
            BackoffStrategy = backoffStrategy
        };

        Assert.Equal("CustomType", config.Type);
        Assert.Equal(breakerStrategy, config.BreakerStrategy);
        Assert.Equal(backoffStrategy, config.BackoffStrategy);
    }

    [Fact]
    public void BreakerStrategyConfig_DefaultValues_ShouldBeNull()
    {
        var strategy = new BreakerStrategyConfig();
        Assert.Null(strategy.Type);
    }

    [Fact]
    public void BreakerStrategyConfig_SetType_ShouldAssignValue()
    {
        var strategy = new BreakerStrategyConfig { Type = "FailureThreshold" };
        Assert.Equal("FailureThreshold", strategy.Type);
    }

    [Fact]
    public void FailureThresholdStrategyConfig_DefaultValues_ShouldBeNull()
    {
        var strategy = new FailureThresholdStrategyConfig();
        Assert.Null(strategy.Type);
        Assert.Null(strategy.Threshold);
    }

    [Fact]
    public void FailureThresholdStrategyConfig_SetProperties_ShouldAssignValues()
    {
        var strategy = new FailureThresholdStrategyConfig
        {
            Type = "FailureThreshold",
            Threshold = 5
        };
        Assert.Equal("FailureThreshold", strategy.Type);
        Assert.Equal(5, strategy.Threshold);
    }

    [Fact]
    public void BackoffStrategyConfig_DefaultValues_ShouldBeNull()
    {
        var backoff = new BackoffStrategyConfig();
        Assert.Null(backoff.Type);
    }

    [Fact]
    public void BackoffStrategyConfig_SetType_ShouldAssignValue()
    {
        var backoff = new BackoffStrategyConfig { Type = "FixedBackoff" };
        Assert.Equal("FixedBackoff", backoff.Type);
    }

    [Fact]
    public void FixedBackoffConfig_DefaultValues_ShouldBeNull()
    {
        var config = new FixedBackoffConfig();
        Assert.Null(config.Type);
        Assert.Null(config.Duration);
    }

    [Fact]
    public void FixedBackoffConfig_SetProperties_ShouldAssignValues()
    {
        var config = new FixedBackoffConfig
        {
            Type = "FixedBackoff",
            Duration = TimeSpan.FromSeconds(10)
        };
        Assert.Equal("FixedBackoff", config.Type);
        Assert.Equal(TimeSpan.FromSeconds(10), config.Duration);
    }

    [Fact]
    public void IncrementalBackoffConfig_DefaultValues_ShouldBeNull()
    {
        var config = new IncrementalBackoffConfig();
        Assert.Null(config.Type);
        Assert.Null(config.MinBackoff);
        Assert.Null(config.Increment);
        Assert.Null(config.MaxIncrements);
    }

    [Fact]
    public void IncrementalBackoffConfig_SetProperties_ShouldAssignValues()
    {
        var config = new IncrementalBackoffConfig
        {
            Type = "IncrementalBackoff",
            MinBackoff = TimeSpan.FromSeconds(1),
            Increment = TimeSpan.FromSeconds(2),
            MaxIncrements = 5
        };
        Assert.Equal("IncrementalBackoff", config.Type);
        Assert.Equal(TimeSpan.FromSeconds(1), config.MinBackoff);
        Assert.Equal(TimeSpan.FromSeconds(2), config.Increment);
        Assert.Equal(5, config.MaxIncrements);
    }
}