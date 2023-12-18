# CircuitBreaker
Circuit breaker implements a circuit breaker that releafs the de developer from implementing complex logic.

It is a flexible implementation using BreakerStrategies and BackoffStrategies to provide multiple choices when implementing a breaker for your circuit.

To use this pattern, consider the following approach:

1. Create a BreakerStrategy passing a BreakerStrategy with a BackoffStrategy.
2. Wrap your code within one of the circuit breaker Wrap methods. Any exception thrown by the code will be considered a failure and will count for the breaker strategy. Normal completion of the code will count as a success. Exceptions
3. Set the OnOpen event listener to be notified when the circuit is open and take action.
4. Set the OnReset event listener to be notified when the circuit is reset and take action.

## CircuitBreaker in the configuration

# :construction: TBD
