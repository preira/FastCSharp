using FastCSharp.RabbitPublisher.Impl;
using FastCSharp.RabbitPublisher.Common;
using FastCSharp.RabbitCommon;
using FastCSharp.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

public class AsyncRabbitPublisherTests
{
    private readonly Mock<IRabbitConnectionPool> poolMock = new();
    private readonly TestLogger logger = new();
    private readonly TestLoggerFactory loggerFactory;
    private readonly RabbitPublisherConfig config;
    private readonly IOptions<RabbitPublisherConfig> options;

    public AsyncRabbitPublisherTests()
    {
        config = new RabbitPublisherConfig
        {
            Timeout = TimeSpan.FromSeconds(1),
            Exchanges = new Dictionary<string, ExchangeConfig?>
            {
                { "ex", new ExchangeConfig { Name = "ex", Queues = new Dictionary<string, string?> { { "q", "q" } }, RoutingKeys = new List<string> { "rk" } } }
            }
        };
        options = Options.Create(config);
        loggerFactory = new(logger);
    }

    [Fact]
    public void Constructor_WithOptions_SetsProperties()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        Assert.NotNull(publisher);
    }

    [Fact]
    public void Constructor_WithConfig_SetsProperties()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, config);
        Assert.NotNull(publisher);
    }

    [Fact]
    public void ForExchange_ValidExchange_Succeeds()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        var result = publisher.ForExchange("ex");
        Assert.Same(publisher, result);
    }

    [Fact]
    public void ForExchange_InvalidExchange_Throws()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        Assert.Throws<KeyNotFoundException>(() => publisher.ForExchange("invalid"));
    }

    [Fact]
    public void ForQueue_ValidQueue_Succeeds()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        publisher.ForExchange("ex");
        var result = publisher.ForQueue("q");
        Assert.Same(publisher, result);
    }

    [Fact]
    public void ForQueue_InvalidQueue_Throws()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        publisher.ForExchange("ex");
        Assert.Throws<KeyNotFoundException>(() => publisher.ForQueue("invalid"));
    }

    [Fact]
    public void ForRouting_ValidKey_Succeeds()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        publisher.ForExchange("ex");
        var result = publisher.ForRouting("rk");
        Assert.Same(publisher, result);
    }

    [Fact]
    public void ForRouting_InvalidKey_Throws()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        publisher.ForExchange("ex");
        Assert.Throws<ArgumentException>(() => publisher.ForRouting("invalid"));
    }

    [Fact]
    public async Task PublishAsync_SingleMessage_Success()
    {
        var channelMock = new Mock<IRabbitChannel>();
        channelMock.Setup(c => c.BasicPublishAsync(It.IsAny<object>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var connMock = new Mock<IRabbitConnection>();
        connMock.Setup(c => c.GetChannelAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(channelMock.Object);
        connMock.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        poolMock.Setup(p => p.GetConnectionAsync(It.IsAny<object>())).ReturnsAsync(connMock.Object);

        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        publisher.ForExchange("ex").ForQueue("q").ForRouting("rk");
        var result = await publisher.PublishAsync("msg");
        Assert.True(result);
    }

    [Fact]
    public async Task PublishAsync_SingleMessage_ChannelClosed_LogsError()
    {
        var channelMock = new Mock<IRabbitChannel>();
        var shutdownEvent = new ShutdownEventArgs(ShutdownInitiator.Application, 404, "Channel not found", "Channel is closed");
        channelMock.Setup(c => c.BasicPublishAsync(It.IsAny<object>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlreadyClosedException(shutdownEvent));

        var connMock = new Mock<IRabbitConnection>();
        connMock.Setup(c => c.GetChannelAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(channelMock.Object);
        connMock.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        poolMock.Setup(p => p.GetConnectionAsync(It.IsAny<object>())).ReturnsAsync(connMock.Object);

        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        publisher.ForExchange("ex").ForQueue("q").ForRouting("rk");
        var result = await publisher.PublishAsync("msg");
        Assert.False(result);
    }

    [Fact]
    public async Task PublishAsync_Batch_Success()
    {
        var channelMock = new Mock<IRabbitChannel>();
        channelMock.Setup(c => c.BasicPublishAsync(It.IsAny<object>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        channelMock.Setup(c => c.NextPublishSeqNoAsync(It.IsAny<object>())).ReturnsAsync(1UL);

        var connMock = new Mock<IRabbitConnection>();
        connMock.Setup(c => c.GetChannelAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(channelMock.Object);
        connMock.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        poolMock.Setup(p => p.GetConnectionAsync(It.IsAny<object>())).ReturnsAsync(connMock.Object);

        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        publisher.ForExchange("ex").ForQueue("q").ForRouting("rk");
        var result = await publisher.PublishAsync(new[] { "a", "b" });
        Assert.True(result);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        publisher.Dispose();
        publisher.Dispose();
    }

    [Fact]
    public async Task ReportHealthStatusAsync_HealthyConnection_ReturnsHealthy()
    {
        var connMock = new Mock<IRabbitConnection>();
        connMock.SetupGet(c => c.IsOpen).Returns(true);
        connMock.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        poolMock.Setup(p => p.GetConnectionAsync(It.IsAny<object>())).ReturnsAsync(connMock.Object);
        poolMock.Setup(p => p.ReportHealthStatusAsync()).ReturnsAsync(new HealthReport("pool", HealthStatus.Healthy));

        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        var report = await publisher.ReportHealthStatusAsync();
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task ReportHealthStatusAsync_NullConnection_ReturnsUnhealthy()
    {
        var connectionMock = new Mock<IRabbitConnection>();
        poolMock.Setup(p => p.GetConnectionAsync(It.IsAny<object>())).ReturnsAsync(connectionMock.Object);
        poolMock.Setup(p => p.ReportHealthStatusAsync()).ReturnsAsync(new HealthReport("pool", HealthStatus.Healthy));

        var publisher = new AsyncRabbitPublisher<string>(poolMock.Object, loggerFactory, options);
        var report = await publisher.ReportHealthStatusAsync();
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }
}


internal class TestLogger : ILogger
{
    public Stack<LogEntry> Stack { get; }
    public bool _isEnabled;
    public TestLogger(bool isEnabled = true)
    {
        _isEnabled = isEnabled;
        Stack = new Stack<LogEntry>();
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _isEnabled;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Stack.Push(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}

internal class LogEntry
{
    public LogLevel LogLevel { get; set; }
    public string Message { get; set; }
    public Exception? Exception { get; set; }
    public object[] Args { get; set; }

    public LogEntry(LogLevel logLevel, string message, Exception? exception, params object[] args)
    {
        LogLevel = logLevel;
        Message = message;
        Exception = exception;
        Args = args;
    }
}

internal class TestLoggerFactory : ILoggerFactory
{
    private readonly ILogger testLogger;

    public TestLoggerFactory(ILogger logger)
    {
        testLogger = logger;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return testLogger;
    }

    public void AddProvider(ILoggerProvider provider)
    {
        // No-op for this test implementation
    }

    public void Dispose()
    {
        // No-op for this test implementation
    }
}