using FastCSharp.Exceptions;
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
public class CircuitBreakerFactory
{
    public static Func<TInput, Task<TResult>> NewAsyncBreaker<TInput, TResult>(
        ILoggerFactory loggerFactory,
        IConfigurationSection? config, 
        Func<TInput, Task<TResult>> onMessage, 
        Action<object>? onOpen = null, 
        Action<object>? onReset = null)
    {
        var logger = loggerFactory.CreateLogger<CircuitBreakerFactory>();
        var breakerSection = config?.GetSection(CircuitBreakerConfig.SectionName);
        var breakerConfig = breakerSection?.Get<CircuitBreakerConfig>();
        if (breakerConfig == null)
        {
            // no configuration means no circuit breaker
            logger.LogWarning("No circuit breaker configuration found. Circuit breaker disabled.");
            return onMessage;
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

        return circuitBreaker.WrapAsync(onMessage);
    }

    private static BreakerStrategy NewBreakerStrategy(IConfigurationSection? configSection, IBackoffStrategy backoffStrategy)
    {
        var strategySection = configSection?.GetSection(BreakerStrategyConfig.SectionName);
        var config = strategySection?.Get<BreakerStrategyConfig>();

        if (config == null || config.Type == null)
        {
            throw new IncorrectInitializationException("BreakerStrategy configuration not found or incorrect.");
        }

        switch (config.Type)
        {
            case FailureThresholdStrategyConfig.SectionName:
                var eventBreakerConfig = strategySection?.Get<FailureThresholdStrategyConfig>();
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
        var config = strategySection?.Get<BackoffStrategyConfig>();

        if (config == null || config.Type == null)
        {
            throw new IncorrectInitializationException("BackoffStrategy configuration not found or incorrect.");
        }
        switch (config.Type)
        {
            case FixedBackoffConfig.SectionName:
                var fixedBackoffConfig = strategySection?.Get<FixedBackoffConfig>();
                if (fixedBackoffConfig?.Duration == null)
                {
                    throw new IncorrectInitializationException($"BackoffStrategy is incorrect for the type {config.Type}.");
                }
                TimeSpan duration = (TimeSpan)fixedBackoffConfig.Duration;
                return new FixedBackoff(duration);
            case IncrementalBackoffConfig.SectionName:
                var incrConf = strategySection?.Get<IncrementalBackoffConfig>();
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