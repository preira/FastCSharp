using Xunit;
using FastCSharp.Circuit_Breaker;

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

    public override void Close() => Callee = Callee.CLOSED;
    public override void Closing() => Callee = Callee.HALF_CLOSED;
    public override void Open(TimeSpan duration) 
    {
        Callee = Callee.OPEN;
        Duration = duration;
    }

}


public class ConsecutiveFailuresBreaker_Tests
{

    static TimeSpan timeout = new TimeSpan(0, 0, 0, 0, 0, 100);
    static readonly long attemptsThreshold = 2;

    [Fact]
    public void CreateNotNullStrategy()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_Tests.CreateOpenImmediatelyStrategy();

        Assert.NotNull(strategy);
    }

    [Fact]
    public void StrategyShouldOpenCircuitBreaker()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_Tests.CreateOpenImmediatelyStrategy();

        TestBreaker breaker = new TestBreaker(strategy);
        strategy.RegisterUncontrolledFailure();
        // should be open
        Assert.Equal<Callee>(Callee.OPEN, breaker.Callee);
        Assert.Equal<TimeSpan>(timeout, breaker.Duration);
    }

    [Fact]
    public void StrategyShouldOpenCircuitAfterThresholdFailures()
    {
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_Tests.CreateOpenImmediatelyStrategy();

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
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_Tests.CreateOpenImmediatelyStrategy();

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
        BreakerStrategy strategy = ConsecutiveFailuresBreaker_Tests.CreateOpenImmediatelyStrategy();

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
