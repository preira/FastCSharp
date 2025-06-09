using FastCSharp.Exceptions;
using FastCSharp.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FastCSharp.CircuitBreaker;

/// <summary>
/// Factory for CircuitBreaker.
/// </summary>
/// <typeparam name="TInput">The type of the input message.</typeparam>
/// <typeparam name="TResult">The type of the result message.</typeparam>
/// <remarks>
/// The CircuitBreakerFactory is a generic class that can be used to create a CircuitBreaker for a given message type.
/// This factory has overloads to create different types of CircuitBreaker with different type os BreakerStrategy and backoff.
/// For example it has a factory method that takes IConfigurationSection, OnMessage, OnOpen and OnReset and returns a Breaker.
/// The factory method will read the configuration and create the appropriate BreakerStrategy and BackoffStrategy.
/// </remarks> 
public static class CircuitBreakerFactory
{
    public static CircuitBreakerBuilder<TInput, TResult> CreateBuilder<TInput, TResult>()
    {
        return new CircuitBreakerBuilder<TInput, TResult>();
    }
}

public class CircuitBreakerBuilder<TInput, TResult>
{
    private ILogger logger = LoggerFactory.Create(builder => ConsoleLoggerExtensions.AddConsole(builder)).CreateLogger(typeof(CircuitBreakerBuilder<TInput, TResult>));
    private IConfigurationSection? config;
    private Func<TInput, Task<TResult>>? originalCircuit;
    private Action<object>? onOpen;
    private Action<object>? onReset;

    public IHealthReporter HealthReporter { get; private set; } = new EmptyHealthReporter("CircuitBreaker", "Not configured");

    private bool isBuilt = false;
    private Func<TInput, Task<TResult>>? wrappedCircuit;
    public Func<TInput, Task<TResult>> WrappedCircuit {
        get {
            if(!isBuilt) 
            {
                Build();
                isBuilt = true;
            }
            return wrappedCircuit ?? originalCircuit ?? throw new ArgumentNullException("circuit", "Unable to build Circuitbreaker. This is a fatal unexpected exception.");
        }
    }

    public CircuitBreakerBuilder<TInput, TResult> Set(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<CircuitBreakerBuilder<TInput, TResult>>();
        return this;
    }

    public CircuitBreakerBuilder<TInput, TResult> Set(IConfigurationSection? config)
    {
        this.config = config;
        return this;
    }

    public CircuitBreakerBuilder<TInput, TResult> Set(Func<TInput, Task<TResult>>? circuit)
    {
        this.originalCircuit = circuit;
        return this;
    }

    public CircuitBreakerBuilder<TInput, TResult> OnOpen(Action<object>? onOpen)
    {
        this.onOpen = onOpen;
        return this;
    }

    public CircuitBreakerBuilder<TInput, TResult> OnClose(Action<object>? onReset)
    {
        this.onReset = onReset;
        return this;
    }

    public void Build()
    {
        if (wrappedCircuit == null)
        {
            if (originalCircuit == null)
            {
                throw new IncorrectInitializationException("Build method was called too early and there is no circuit to wrap with this CircuitBreaker.");
            }

            if (config == null)
            {
                throw new IncorrectInitializationException("Build method was called too early and configuration was not yet set for this CircuitBreaker.");
            }
 
        var breakerSection = config.GetSection(CircuitBreakerConfig.SectionName);
        var breakerConfig = breakerSection?.Get<CircuitBreakerConfig>();
        if (breakerConfig == null)
        {
            // no configuration means no circuit breaker
                logger.LogWarning("No circuit breaker configuration found. Circuit breaker disabled. Check you configuration settings if this is unintended.");
                return;
        }
        var breakerStrategy = NewBreakerStrategy(breakerSection, NewBackoffStrategy(breakerSection));

        AbstractBreaker circuitBreaker;
        switch (breakerConfig.Type)
        {
            case BlockingCircuitBreaker.TypeName:
                circuitBreaker = new BlockingCircuitBreaker(breakerStrategy);
                break;
            case EventDrivenCircuitBreaker.TypeName:
                EventDrivenCircuitBreaker eventBreaker = new EventDrivenCircuitBreaker(breakerStrategy);
                if (onOpen == null || onReset == null)
                {
                    throw new IncorrectInitializationException("EventDrivenCircuitBreaker requires onOpen and onReset callbacks. Review your implementation or your configuration.");
                }
                eventBreaker.OnOpen += onOpen;
                eventBreaker.OnReset += onReset;
                circuitBreaker = eventBreaker;
                break;
            default:
                circuitBreaker = new CircuitBreaker(breakerStrategy);
                break;
        }

            HealthReporter = circuitBreaker;
            wrappedCircuit = circuitBreaker.WrapAsync(originalCircuit);
    }
    }


    private static BreakerStrategy NewBreakerStrategy(IConfigurationSection? configSection, IBackoffStrategy backoffStrategy)
    {
        var strategySection = configSection?.GetSection(BreakerStrategyConfig.SectionName);
        if (strategySection == null)
        {
            throw new IncorrectInitializationException("BreakerStrategy configuration section not found.");
        }

        var config = strategySection.Get<BreakerStrategyConfig>();

        if (config == null || config.Type == null || strategySection == null)
        {
            throw new IncorrectInitializationException("BreakerStrategy configuration seems to be incorrect.");
        }

        switch (config.Type)
        {
            case FailureThresholdStrategyConfig.SectionName:
                var eventBreakerConfig = strategySection.Get<FailureThresholdStrategyConfig>();
                if (eventBreakerConfig?.Threshold == null)
                {
                    throw new IncorrectInitializationException($"BreakerStrategy is incorrect for the type {config.Type}.");
                }
                return new FailuresThresholdBreakerStrategy((int)eventBreakerConfig.Threshold, backoffStrategy);
            default:
                throw new IncorrectInitializationException("BreakerStrategy type not found.");
        }
    }

    private static IBackoffStrategy NewBackoffStrategy(IConfigurationSection? configSection)
    {
        var strategySection = configSection?.GetSection(BackoffStrategyConfig.SectionName);
        if (strategySection == null)
        {
            throw new IncorrectInitializationException("BackoffStrategy configuration not found.");
        }

        var config = strategySection.Get<BackoffStrategyConfig>();
        if (config == null || config.Type == null)
        {
            throw new IncorrectInitializationException("BackoffStrategy configuration seems to be incorrect.");
        }
        switch (config.Type)
        {
            case FixedBackoffConfig.SectionName:
                var fixedBackoffConfig = strategySection.Get<FixedBackoffConfig>();
                if (fixedBackoffConfig?.Duration == null)
                {
                    throw new IncorrectInitializationException($"BackoffStrategy is incorrect for the type {config.Type}.");
                }
                TimeSpan duration = (TimeSpan)fixedBackoffConfig.Duration;
                return new FixedBackoff(duration);
            case IncrementalBackoffConfig.SectionName:
                var incrConf = strategySection.Get<IncrementalBackoffConfig>();
                if (incrConf?.Increment == null || incrConf.MaxIncrements == null || incrConf.MinBackoff == null)
                {
                    throw new IncorrectInitializationException($"BackoffStrategy is incorrect for the type {config.Type}.");
                }
                var increments = (TimeSpan)incrConf.Increment;
                var minBackoff = (TimeSpan)incrConf.MinBackoff;
                var maxIncrements = (long)incrConf.MaxIncrements;
                return new IncrementalBackoff(minBackoff, increments, maxIncrements);
            default:
                throw new IncorrectInitializationException($"BackoffStrategy type '{config.Type}' not found.");
        }
    }
}