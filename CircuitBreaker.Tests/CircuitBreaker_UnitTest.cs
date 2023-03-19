using Xunit;
using FastCSharp.CircuitBreaker;
using FastCSharp;

namespace Circuit_Breaker.Tests;

public static class Util
{
    static TimeSpan increment = new TimeSpan(0, 0, 0, 0, 0, 100);
    static readonly long attemptsThreshold = 2;
    public readonly static TimeSpan _millisec_backoff = new TimeSpan(0, 0, 0, 0, 5);
    public static bool ExecuteThrowNotImplementedException(AbstractBreaker circuit, bool Success)
    {
        Assert.Throws<NotImplementedException>(
            () => circuit.Wrap(
                () =>
                {
                    Success = true;
                    throw new NotImplementedException();
                })
            );
        return Success;
    }

    public static bool ExecuteThrowingCircuitException(AbstractBreaker circuit, bool Success)
    {
        Assert.Throws<CircuitException>(
            () => circuit.Wrap(
                () =>
                {
                    Success = true;
                    throw new CircuitException();
                })
            );
        return Success;
    }
}

public class CircuitBreaker_Tests
{
    [Fact]
    public void CreateNonNullCircuit()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        circuit.Wrap(() => { /*no need to implement*/ });
        Assert.NotNull(circuit);
    }

    [Fact]
    public void SuccessfulExecutionCircuit()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Boolean Success = false;
        circuit.Wrap( () => Success = true );
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void ControlledFailureExecutionCircuit()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");

        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed(), "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void UnControlledFailureExecutionCircuit()
    {
        TimeSpan timeout = new TimeSpan(0, 0, 10);
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(timeout), true)
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");

        Boolean Success = false;
        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be Open.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void UnControlledFailureExecutionCircuitWithByPass()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");

        Boolean Success = false;
        for (var i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed(), "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void ControlledFailureRecovery()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed(), "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");

        Assert.Throws<OpenCircuitException>(() => circuit.Wrap(() => Success = false));
        Assert.True(Success, "Function executed and shouldn't!");
        Thread.Sleep(Util._millisec_backoff);

        Success = false;
        circuit.Wrap(() => Success = true);
        Assert.True(Success, "Function dind't execute after timeout!");
    }

    [Fact]
    public void UnControlledFailureRecovery()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed(), "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");

        Assert.Throws<OpenCircuitException>(() => circuit.Wrap(() => Success = false));
        Assert.True(Success, "Function executed and shouldn't!");

        Thread.Sleep(Util._millisec_backoff);
        Success = false;
        circuit.Wrap(() => Success = true);
        Assert.True(Success, "Function dind't execute after timeout!");
    }
}

public class BlockingCircuitBreaker_Tests
{

    static TimeSpan increment = new TimeSpan(0, 0, 0, 0, 0, 100);
    [Fact]
    public void CreateNonNullCircuit()
    {
        var circuit =
            new BlockingCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        circuit.Wrap(() =>{ /*no need to implement*/ });
        Assert.NotNull(circuit);
    }

    [Fact]
    public void SuccessfulExecutionCircuit()
    {
        var circuit =
            new BlockingCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Boolean Success = false;
        circuit.Wrap(() => Success = true);
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void ControlledFailureExecutionCircuit()
    {
        var circuit =
            new BlockingCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed(), "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void UnControlledFailureExecutionCircuit()
    {
        TimeSpan timeout = new TimeSpan(0, 0, 10);
        var circuit =
            new BlockingCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(timeout), true)
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");

        Boolean Success = false;
        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be Open.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void UnControlledFailureExecutionCircuitWithByPass()
    {
        var circuit =
            new BlockingCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed(), "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void ControlledFailureRecovery()
    {
        DateTime startTime = DateTime.Now;
        var circuit =
            new BlockingCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed(), "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");

        Thread.Sleep(Util._millisec_backoff);
        Success = false;
        circuit.Wrap(() => Success = true);
        TimeSpan elapsedTime = DateTime.Now - startTime;
        Assert.True(elapsedTime > Util._millisec_backoff, $"Elapsed Time {elapsedTime} > backoff {Util._millisec_backoff}");
        Assert.True(Success, "Function dind't execute after timeout!");
    }

    [Fact]
    public void UnControlledFailureRecovery()
    {
        DateTime startTime = DateTime.Now;
        var circuit =
            new BlockingCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed(), "Circuit should start Closed.");
        Boolean Success = false;
        for (var i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed(), "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen(), "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");

        // Will block until backoff is cleared. It serves as backoff control and will fail.
        Assert.Throws<OpenCircuitException>(
            () => circuit.Wrap(
                () => Success = true)
            );
        TimeSpan elapsedTime = DateTime.Now - startTime;
        Assert.True(elapsedTime > Util._millisec_backoff, $"Elapsed Time {elapsedTime} > backoff {Util._millisec_backoff}");
        Success = false;
        circuit.Wrap(() => Success = true);
        Assert.True(Success, "Function dind't execute after timeout!");
    }
}
