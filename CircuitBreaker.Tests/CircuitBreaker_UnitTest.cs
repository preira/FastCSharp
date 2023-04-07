using Xunit;
using FastCSharp.CircuitBreaker;
using FastCSharp;

namespace Circuit_Breaker.Tests;

public static class Util
{
    static TimeSpan increment = new TimeSpan(0, 0, 0, 0, 0, 100);
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

public class CircuitBreaker_UnitTest
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
    public void IsHalfClosed_Coverage()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.NotNull(circuit);
        Assert.False(circuit.IsHalfclosed);
        circuit.Closing();
        Assert.True(circuit.IsHalfclosed);
        circuit.Open(Util._millisec_backoff);
        Assert.False(circuit.IsHalfclosed);
    }

    [Fact]
    public void SuccessfulExecutionCircuit()
    {
        var circuit =
            new CircuitBreaker(
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
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");

        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
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

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");

        Boolean Success = false;
        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be Open.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void UnControlledFailureExecutionCircuitWithByPass()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");

        Boolean Success = false;
        for (var i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void ControlledFailureRecovery()
    {
        var circuit =
            new CircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
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

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
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

        circuit.Wrap(() => { /*no need to implement*/ });
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

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
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

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");

        Boolean Success = false;
        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be Open.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void UnControlledFailureExecutionCircuitWithByPass()
    {
        var circuit =
            new BlockingCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
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

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
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

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");
        Boolean Success = false;
        for (var i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
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


public class EventDrivenCircuitBreaker_UnitTest
{
    [Fact]
    public void CreateNonNullCircuit()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        circuit.Wrap(() => { /*no need to implement*/ });
        Assert.NotNull(circuit);
    }

    [Fact]
    public void IsHalfClosed_Coverage()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.NotNull(circuit);
        Assert.False(circuit.IsHalfclosed);
        circuit.Closing();
        Assert.True(circuit.IsHalfclosed);
        circuit.Open(Util._millisec_backoff);
        Assert.False(circuit.IsHalfclosed);
    }

    [Fact]
    public void Circuit_OnClose()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        var closing = false;
        circuit.OnReset += (sender) => { closing = true; };
        Assert.False(closing);
        circuit.Closing();
        Assert.True(closing);
    }

    [Fact]
    public void CircuitOnClose_MultipleListenners()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        bool[] closed = { false, false, false };
        for (int i = 0; i < closed.Length; i++)
        {
            int index = i;
            circuit.OnReset += (sender) => { closed[index] = true; };
            Assert.False(closed[index]);
        }
        circuit.Closing();
        Array.ForEach(closed, elem => Assert.True(elem));
    }

    [Fact]
    public void Circuit_OnOpen()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        var opened = false;
        circuit.OnOpen += (sender) => { opened = true; };
        Assert.False(opened);
        Assert.False(circuit.IsOpen);
        circuit.Open(Util._millisec_backoff);
        Assert.True(opened);
    }

    [Fact]
    public void AttemptRecovery_Test()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        var opened = false;
        var reseted = false;
        circuit.OnOpen += (sender) => { opened = true; };
        circuit.OnReset += (sender) => { reseted = true; };
        Assert.False(opened);

        circuit.Open(new TimeSpan(0));
        Assert.True(opened);
        Task.Delay(1).Wait();
        Assert.True(reseted);
    }

    [Fact]
    public void AttemptRecovery_FailBeforeRecovering_Test()
    {
        var _backoff = new TimeSpan(1, 0, 0, 0, 5);
        var minimalDelay = new TimeSpan(0, 0, 0, 0, 3);
        var circuit =
            new EventDrivenCircuitBreaker(
                // huge timeout to control recovery through CancelBackoff
                new ConsecutiveFailuresBreakerStrategy(2, new FixedBackoff(_backoff))
            );

        var opened = false;
        circuit.OnOpen += (sender) => { opened = true; };
        Assert.False(opened);

        bool[] attemptRecoveries = { false, false, false };

        var recoveryScript = new List<Func<bool>>()
        {
            () => { attemptRecoveries[0] = true; throw new Exception("Test Exception 4");  },
            () => { attemptRecoveries[1] = true; throw new Exception("Test Exception 5"); },
            () => { attemptRecoveries[2] = true; return true; }
        };
        var attemptStep = recoveryScript.GetEnumerator();
        circuit.OnReset += (sender) =>
        {
            try
            {
                circuit.Wrap<bool>(() => attemptStep.MoveNext() ? attemptStep.Current() : false);
            }
            catch (Exception) { }
        };
        // increment breaker counter
        try
        {
            circuit.Wrap<bool>(() => throw new Exception("Test Exception 1"));
        }
        catch (Exception) { }
        try
        {
            circuit.Wrap<bool>(() => throw new Exception("Test Exception 2"));
        }
        catch (Exception) { }

        AssertAttemptRecoveries(attemptRecoveries, new bool[] { false, false, false });

        // Open the circuit
        try
        {
            circuit.Wrap<bool>(() => throw new Exception("Test Exception 3"));
        }
        catch (Exception) { }
        // circuit.Open(new TimeSpan(10000000000));
        AssertAttemptRecoveries(attemptRecoveries, new bool[] { false, false, false });

        circuit.CancelBackoff();
        Task.Delay(minimalDelay).Wait();
        Assert.True(opened);
        AssertAttemptRecoveries(attemptRecoveries, new bool[] { true, false, false });

        circuit.CancelBackoff();
        Task.Delay(minimalDelay).Wait();
        Assert.True(opened);
        AssertAttemptRecoveries(attemptRecoveries, new bool[] { true, true, false });

        // Finally succeeds
        circuit.CancelBackoff();
        Task.Delay(minimalDelay).Wait();
        AssertAttemptRecoveries(attemptRecoveries, new bool[] { true, true, true });

        Assert.True(circuit.IsClosed);

        static void AssertAttemptRecoveries(bool[] actual, bool[] expected)
        {
            Assert.Equal(expected[0], actual[0]);
            Assert.Equal(expected[1], actual[1]);
            Assert.Equal(expected[2], actual[2]);
        }
    }

    [Fact]
    public void SuccessfulExecutionCircuit()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
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
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");

        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void UnControlledFailureExecutionCircuit()
    {
        TimeSpan timeout = new TimeSpan(0, 0, 0, 0, 2);
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(timeout), true)
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");

        Boolean Success = false;
        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be Open.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void Test_RemoveListeners()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        var opened = false;
        var reseted = false;
        Action<object> onOpenListener = (sender) => { opened = true; };
        Action<object> onResetListener = (sender) => { reseted = true; };
        circuit.OnOpen += onOpenListener;
        circuit.OnReset += onResetListener;
        Assert.False(opened);
        Assert.False(reseted);

        circuit.Open(new TimeSpan(0, 0, 0, 0, 1));
        Assert.True(opened);
        Assert.False(reseted);

        circuit.Closing();
        Assert.True(opened);
        Assert.True(reseted);

        opened = false;
        reseted = false;

        circuit.OnOpen -= onOpenListener;
        circuit.OnReset -= onResetListener;

        circuit.Open(new TimeSpan(0, 0, 0, 0, 1));
        Assert.False(opened);
        Assert.False(reseted);

        circuit.Close();
        Assert.False(opened);
        Assert.False(reseted);

        circuit.Closing();
        Assert.False(opened);
        Assert.False(reseted);
    }

    [Fact]
    public void UnControlledFailureExecutionCircuitWithByPass()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");

        Boolean Success = false;
        for (var i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");
    }

    [Fact]
    public void ControlledFailureRecovery()
    {
        TimeSpan timeout = new TimeSpan(0, 0, 0, 0, 20);
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(timeout))
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowingCircuitException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");

        Assert.Throws<OpenCircuitException>(() => circuit.Wrap(() => Success = false));
        Assert.True(Success, "Function executed and shouldn't!");
        Thread.Sleep(timeout);

        Success = false;
        circuit.Wrap(() => Success = true);
        Assert.True(Success, "Function dind't execute after timeout!");
    }

    [Fact]
    public void UnControlledFailureRecovery()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );

        Assert.True(circuit.IsClosed, "Circuit should start Closed.");
        Boolean Success = false;
        for (int i = 0; i < 5; ++i)
        {
            Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        }
        Assert.True(circuit.IsClosed, "Circuit should remain Closed.");

        Success = Util.ExecuteThrowNotImplementedException(circuit, Success);
        Assert.True(circuit.IsOpen, "Circuit should be open now.");
        Assert.True(Success, "Function dind't execute!");

        Assert.Throws<OpenCircuitException>(() => circuit.Wrap(() => Success = false));
        Assert.True(Success, "Function executed and shouldn't!");

        Thread.Sleep(Util._millisec_backoff);
        Success = false;
        circuit.Wrap(() => Success = true);
        Assert.True(Success, "Function dind't execute after timeout!");
    }

    [Fact]
    public void Test_SecondOpen()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );
        Assert.True(circuit.Open(new TimeSpan(0, 0, 0, 0, 1)), "Circuit should open.");
        Assert.False(circuit.Open(new TimeSpan(0, 0, 0, 0, 1)), "Circuit should already be open.");
    }

    [Fact]
    public void Test_CancelBackoffWhenClosed()
    {
        var circuit =
            new EventDrivenCircuitBreaker(
                new ConsecutiveFailuresBreakerStrategy(5, new FixedBackoff(Util._millisec_backoff))
            );
        Assert.False(circuit.CancelBackoff(), "Should not have canceled backoff.");

    }
}

public class CircuitException_Tests
{
    [Fact]
    public void Test_CircuitException()
    {
        var exception = new CircuitException();
        Assert.NotNull(exception);
    }

    [Fact]
    public void Test_CircuitException_WithInnerException()
    {
        var innerException = new Exception("Inner");
        var exception = new CircuitException("Test", innerException);
        Assert.Equal("Test", exception.Message);
        Assert.Equal("Inner", exception.InnerException.Message);
    }

    [Fact]
    public void Test_CircuitExceptionWithMessage()
    {
        var exception = new CircuitException("Test");
        Assert.Equal("Test", exception.Message);
    }
}

public class OpenCircuitException_Tests
{
    [Fact]
    public void Test_OpenCircuitException()
    {
        var exception = new OpenCircuitException();
        Assert.NotNull(exception);
    }

    [Fact]
    public void Test_OpenCircuitException_WithInnerException()
    {
        var innerException = new Exception("Inner");
        var exception = new OpenCircuitException("Test", innerException);
        Assert.Equal("Test", exception.Message);
        Assert.Equal("Inner", exception.InnerException.Message);
    }

    [Fact]
    public void Test_OpenCircuitExceptionWithMessage()
    {
        var exception = new OpenCircuitException("Test");
        Assert.Equal("Test", exception.Message);
    }
}