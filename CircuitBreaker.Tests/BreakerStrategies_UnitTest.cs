using Xunit;
using FastCSharp.CircuitBreaker;
using FastCSharp.Exceptions;

namespace Circuit_Breaker.Tests;

enum Callee
{
    OPEN,
    CLOSED,
    HALF_CLOSED,
}

class TestBreaker : Breaker
{
    public Callee Callee { get; set; }
    public TimeSpan Duration { get; set; }
    public TestBreaker(BreakerStrategy strategy) : base(strategy)
    {
        Close();
    }

    public override bool Close()
    {
        var previousStatus = Callee;
        Callee = Callee.CLOSED;
        return previousStatus != Callee;
    }

    public override bool Closing()
    {
        Callee = Callee.HALF_CLOSED;
        return true;
    }
    public override bool Open(TimeSpan duration)
    {
        var previousStatus = Callee;
        Callee = Callee.OPEN;
        Duration = duration;
        return previousStatus != Callee;
    }

}


public class ConsecutiveFailuresBreaker_UnitTests
{

    static TimeSpan timeout = new TimeSpan(1000); // 1000 x 100 nanos = 100 microseconds
    static readonly long attemptsThreshold = 2;

    [Fact]
    public void CreateNotNullStrategy()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_UnitTests.CreateOpenImmediatelyStrategy();

        Assert.NotNull(strategy);
    }

    [Fact]
    public void StrategyShouldOpenCircuitBreaker()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_UnitTests.CreateOpenImmediatelyStrategy();

        TestBreaker breaker = new TestBreaker(strategy);
        strategy.RegisterUncontrolledFailure();
        // should be open
        Assert.Equal<Callee>(Callee.OPEN, breaker.Callee);
        Assert.Equal<TimeSpan>(timeout, breaker.Duration);
    }

    [Fact]
    public void StrategyShouldOpenCircuitAfterThresholdFailures()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_UnitTests.CreateOpenImmediatelyStrategy();

        TestBreaker breaker = new TestBreaker(strategy);
        for (int i = 0; i < attemptsThreshold; ++i)
        {
            strategy.RegisterFailure();
            // should be closed
            Assert.Equal<Callee>(Callee.CLOSED, breaker.Callee);
        }

        strategy.RegisterFailure();
        // should be open
        Assert.Equal<Callee>(Callee.OPEN, breaker.Callee);
        Assert.Equal<TimeSpan>(timeout, breaker.Duration);
    }

    [Fact]
    public void StrategyShouldContinueOpenAfterMoreThanThresholdFailures()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_UnitTests.CreateOpenImmediatelyStrategy();

        TestBreaker breaker = new TestBreaker(strategy);
        int i = 0;
        for (; i < attemptsThreshold; ++i)
        {
            strategy.RegisterFailure();
            // should be closed
            Assert.Equal<Callee>(Callee.CLOSED, breaker.Callee);
        }

        for (; i < 10; ++i)
        {
            strategy.RegisterFailure();
            // should be open
            Assert.Equal<Callee>(Callee.OPEN, breaker.Callee);
            Assert.Equal<TimeSpan>(timeout, breaker.Duration);
        }
    }

    [Fact]
    public void StrategyShouldCloseCircuit()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_UnitTests.CreateOpenImmediatelyStrategy();

        TestBreaker breaker = new TestBreaker(strategy);
        strategy.RegisterUncontrolledFailure();
        // should be open
        Assert.Equal<Callee>(Callee.OPEN, breaker.Callee);
        Assert.Equal<TimeSpan>(timeout, breaker.Duration);

        strategy.RegisterSucess();
        // should be closed
        Assert.Equal<Callee>(Callee.CLOSED, breaker.Callee);
    }

    [Fact]
    public void DontRegisterBreaker()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_UnitTests.CreateOpenImmediatelyStrategy();

        Assert.Throws<IncorrectInitializationException>(() => strategy.RegisterUncontrolledFailure());
        Assert.Throws<IncorrectInitializationException>(() => strategy.RegisterSucess());
        Assert.Throws<IncorrectInitializationException>(() => strategy.RegisterFailure());
    }

    [Fact]
    public void StrategyShouldByPassUncontrolledFailures()
    {
        IBackoffStrategy backoff = new FixedBackoff(timeout);
        BreakerStrategy strategy
            = new ConsecutiveFailuresBreakerStrategy(attemptsThreshold, backoff);

        TestBreaker breaker = new TestBreaker(strategy);
        int i = 0;
        for (; i < attemptsThreshold; ++i)
        {
            strategy.RegisterUncontrolledFailure();
            // should be closed
            Assert.Equal<Callee>(Callee.CLOSED, breaker.Callee);
        }

        for (; i < 10; ++i)
        {
            strategy.RegisterUncontrolledFailure();
            // should be open
            Assert.Equal<Callee>(Callee.OPEN, breaker.Callee);
            Assert.Equal<TimeSpan>(timeout, breaker.Duration);
        }
    }

    private static BreakerStrategy CreateOpenImmediatelyStrategy()
    {
        IBackoffStrategy backoff = new FixedBackoff(timeout);
        BreakerStrategy startegy
            = new ConsecutiveFailuresBreakerStrategy(attemptsThreshold, backoff, true);
        return startegy;
    }
}
