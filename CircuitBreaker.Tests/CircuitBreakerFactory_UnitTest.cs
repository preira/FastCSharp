using FastCSharp.Circuit.Breaker;
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

}