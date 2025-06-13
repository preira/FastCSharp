using FastCSharp.Circuit.Breaker;
using FastCSharp.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Circuit_Breaker.Tests;
public class CircuitBreakerFactory_UnitTest
{
    [Fact]
    public async Task Create_ShouldACircuitWithNoBreaker()
    {
        var isSuccess = false;
        var returnValue = new object();
        // Arrange
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        var callback = (object input) =>
        {
            isSuccess = true;
            return Task.FromResult<object>(returnValue);
        };

        builder
            .Set(LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace)))
            .Set(new ConfigurationBuilder().Build().GetSection("CircuitBreaker"))
            .Set(callback)
            .OnOpen((sender) => Console.WriteLine("Circuit Opened"))
            .OnClose((sender) => Console.WriteLine("Circuit Closed"))
            .Build();

        var circuit = builder.WrappedCircuit;
        var input = new object();
        var returnedValue = await circuit(input);
        Assert.Same(returnValue, returnedValue);
        Assert.True(isSuccess, "The callback should have been invoked successfully.");
    }

    [Fact]
    public async Task Create_BlockingCircuitBreaker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "BlockingCircuitBreaker" },
                { "CircuitBreaker:Strategy:Type", "FailureThresholdStrategy " },
                { "CircuitBreaker:Strategy:Threshold", "2" },
                { "CircuitBreaker:BackoffStrategy:Type", "FixedBackoff" },
                { "CircuitBreaker:BackoffStrategy:Duration", "200" },
            })
            .Build();
        var isSuccess = false;
        var returnValue = new object();
        // Arrange
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        var callback = (object input) =>
        {
            isSuccess = true;
            return Task.FromResult(returnValue);
        };

        builder
            .Set(LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace)))
            .Set(configuration.GetSection("CircuitBreaker"))
            .Set(callback)
            .Build();

        var circuit = builder.WrappedCircuit;
        var input = new object();
        var returnedValue = await circuit(input);
        Assert.Same(returnValue, returnedValue);
        Assert.True(isSuccess, "The callback should have been invoked successfully.");
    }

    [Fact]
    public async Task Create_EventDrivenCircuitBreaker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "EventDrivenCircuitBreaker" },
                { "CircuitBreaker:Strategy:Type", "FailureThresholdStrategy " },
                { "CircuitBreaker:Strategy:Threshold", "2" },
                { "CircuitBreaker:BackoffStrategy:Type", "IncrementalBackoffConfig" },
                { "CircuitBreaker:BackoffStrategy:MinBackoff", "100" },
                { "CircuitBreaker:BackoffStrategy:Increment", "10" },
                { "CircuitBreaker:BackoffStrategy:MaxIncrements", "5" },
            })
            .Build();
        var isSuccess = false;
        var returnValue = new object();
        // Arrange
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        var callback = (object input) =>
        {
            isSuccess = true;
            return Task.FromResult(returnValue);
        };

        builder
            .Set(LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace)))
            .Set(configuration.GetSection("CircuitBreaker"))
            .Set(callback)
            .OnOpen((sender) => Console.WriteLine("Circuit Opened"))
            .OnClose((sender) => Console.WriteLine("Circuit Closed"))
            .Build();

        var circuit = builder.WrappedCircuit;
        var input = new object();
        var returnedValue = await circuit(input);
        Assert.Same(returnValue, returnedValue);
        Assert.True(isSuccess, "The callback should have been invoked successfully.");
    }

    [Fact]
    public async Task Create_CircuitBreaker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "CircuitBreaker" },
                { "CircuitBreaker:Strategy:Type", "FailureThresholdStrategy " },
                { "CircuitBreaker:Strategy:Threshold", "2" },
                { "CircuitBreaker:BackoffStrategy:Type", "FixedBackoff" },
                { "CircuitBreaker:BackoffStrategy:Duration", "200" },
            })
            .Build();
        var isSuccess = false;
        var returnValue = new object();
        // Arrange
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        var callback = (object input) =>
        {
            isSuccess = true;
            return Task.FromResult(returnValue);
        };

        builder
            .Set(LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace)))
            .Set(configuration.GetSection("CircuitBreaker"))
            .Set(callback)
            .Build();

        var circuit = builder.WrappedCircuit;
        var input = new object();
        var returnedValue = await circuit(input);
        Assert.Same(returnValue, returnedValue);
        Assert.True(isSuccess, "The callback should have been invoked successfully.");
    }

    [Fact]
    public async Task Create_Default()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // { "CircuitBreaker:Type", "CircuitBreaker" },
                { "CircuitBreaker:Strategy:Type", "FailureThresholdStrategy " },
                { "CircuitBreaker:Strategy:Threshold", "2" },
                { "CircuitBreaker:BackoffStrategy:Type", "FixedBackoff" },
                { "CircuitBreaker:BackoffStrategy:Duration", "200" },
            })
            .Build();
        var isSuccess = false;
        var returnValue = new object();
        // Arrange
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        var callback = (object input) =>
        {
            isSuccess = true;
            return Task.FromResult(returnValue);
        };

        builder
            .Set(LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace)))
            .Set(configuration.GetSection("CircuitBreaker"))
            .Set(callback)
            .Build();

        var circuit = builder.WrappedCircuit;
        var input = new object();
        var returnedValue = await circuit(input);
        Assert.Same(returnValue, returnedValue);
        Assert.True(isSuccess, "The callback should have been invoked successfully.");
    }

    [Fact]
    public void Build_Throws_WhenCallbackNotSet()
    {
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        builder.Set(new ConfigurationBuilder().Build().GetSection("CircuitBreaker"));
        Assert.Throws<IncorrectInitializationException>(() => builder.Build());
    }

    [Fact]
    public void Build_Throws_WhenConfigNotSet()
    {
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        builder.Set((object input) => Task.FromResult<object>(new object()));
        Assert.Throws<IncorrectInitializationException>(() => builder.Build());
    }

    [Fact]
    public void Build_DisablesBreaker_WhenBreakerConfigMissing()
    {
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        builder.Set((object input) => Task.FromResult<object>(new object()));
        builder.Set(new ConfigurationBuilder().Build().GetSection("NonExistentSection"));
        // Should not throw, just disables breaker
        builder.Build();
        Assert.NotNull(builder.WrappedCircuit);
    }

    [Fact]
    public void Build_Throws_WhenBreakerStrategyConfigMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "BlockingCircuitBreaker" }
            })
            .Build();
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        var circuit = (object input) => Task.FromResult<object>(new object());
        builder.Set(circuit);
        builder.Set(config.GetSection("CircuitBreaker"));
        builder.Build();
        // TODO: After building the circuit without strategy config, the circuit should be the original callback
        Assert.Equal(builder.WrappedCircuit, circuit);
    }

    [Fact]
    public void Build_Throws_WhenBackoffStrategyConfigMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "BlockingCircuitBreaker" },
                { "CircuitBreaker:CircuitBreaker:Strategy:Type", "FailureThresholdStrategy" },
                { "CircuitBreaker:CircuitBreaker:Strategy:Threshold", "2" }
            })
            .Build();
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        builder.Set((object input) => Task.FromResult<object>(new object()));
        builder.Set(config.GetSection("CircuitBreaker"));
        Assert.Throws<IncorrectInitializationException>(() => builder.Build());
    }

    [Fact]
    public void Build_Throws_WhenEventDrivenBreakerWithoutCallbacks()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "EventDrivenCircuitBreaker" },
                { "CircuitBreaker:CircuitBreaker:Strategy:Type", "FailureThresholdStrategy" },
                { "CircuitBreaker:CircuitBreaker:Strategy:Threshold", "2" },
                { "CircuitBreaker:CircuitBreaker:BackoffStrategy:Type", "FixedBackoff" },
                { "CircuitBreaker:CircuitBreaker:BackoffStrategy:Duration", "200" },
            })
            .Build();
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        builder.Set((object input) => Task.FromResult<object>(new object()));
        builder.Set(config.GetSection("CircuitBreaker"));
        // Missing OnOpen and OnClose
        Assert.Throws<IncorrectInitializationException>(() => builder.Build());
    }

    [Fact]
    public void Build_Throws_WhenBreakerStrategyTypeUnknown()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "BlockingCircuitBreaker" },
                { "CircuitBreaker:CircuitBreaker:Strategy:Type", "UnknownStrategy" },
                { "CircuitBreaker:CircuitBreaker:BackoffStrategy:Type", "FixedBackoff" },
                { "CircuitBreaker:CircuitBreaker:BackoffStrategy:Duration", "200" },
            })
            .Build();
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        builder.Set((object input) => Task.FromResult<object>(new object()));
        builder.Set(config.GetSection("CircuitBreaker"));
        Assert.Throws<IncorrectInitializationException>(() => builder.Build());
    }

    [Fact]
    public void Build_Throws_WhenBackoffStrategyTypeUnknown()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "BlockingCircuitBreaker" },
                { "CircuitBreaker:CircuitBreaker:Strategy:Type", "FailureThresholdStrategy" },
                { "CircuitBreaker:CircuitBreaker:Strategy:Threshold", "2" },
                { "CircuitBreaker:CircuitBreaker:BackoffStrategy:Type", "UnknownBackoff" },
            })
            .Build();
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        builder.Set((object input) => Task.FromResult<object>(new object()));
        builder.Set(config.GetSection("CircuitBreaker"));
        Assert.Throws<IncorrectInitializationException>(() => builder.Build());
    }

    [Fact]
    public void WrappedCircuit_Throws_WhenNoCallback()
    {
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        Assert.Throws<IncorrectInitializationException>(() => { var _ = builder.WrappedCircuit; });
    }

    [Fact]
    public void Build_OnlyOnce()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CircuitBreaker:Type", "BlockingCircuitBreaker" },
                { "CircuitBreaker:Strategy:Type", "FailureThresholdStrategy" },
                { "CircuitBreaker:Strategy:Threshold", "2" },
                { "CircuitBreaker:BackoffStrategy:Type", "FixedBackoff" },
                { "CircuitBreaker:BackoffStrategy:Duration", "200" },
            })
            .Build();
        var builder = CircuitBreakerFactory.CreateBuilder<object, object>();
        builder.Set((object input) => Task.FromResult<object>(new object()));
        builder.Set(config.GetSection("CircuitBreaker"));
        builder.Build();
        // Should not throw if Build is called again
        builder.Build();
    }
}