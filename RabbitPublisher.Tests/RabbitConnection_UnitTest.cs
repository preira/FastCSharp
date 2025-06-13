using Moq;
using Xunit;
using RabbitMQ.Client;
using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.Logging;
using FastCSharp.Pool;

namespace RabbitPublisher.Tests;

public class RabbitConnection_UnitTest
{
    [Fact]
    public async Task Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("NonHostConsoleApp.Program", LogLevel.Debug)
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole();
        });

        var connectionMock = new Mock<IConnection>();

        var channelMock = new Mock<IChannel>();
        // FIXME: Ensure that the channel mock is set up correctly
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        // Act
        var connection = new RabbitConnection(
            connectionMock.Object,
            loggerFactory,
            new PoolConfig
            {
                MinSize = 1,
                MaxSize = 10,
                DefaultWaitTimeout = TimeSpan.FromSeconds(1),
                GatherStats = false,
                Initialize = false
            }
            );
        Assert.False(connection.IsStalled);
        var channel = await connection.GetChannelAsync(this, "myExchange", "myQueue", "myRoutingKey");
        Assert.NotNull(channel);
        var channel2 = await connection.GetChannelAsync(this, "myExchange", "myQueue", "myRoutingKey");
        Assert.NotNull(channel2);
        await connection.CloseAsync();
        Assert.False(connection.IsOpen);

        // Assert
        Assert.NotNull(connection);
        await connection.DisposeValue();
        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance2()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var config = new RabbitPublisherConfig
        {
            ClientName = "TestClient",
            Pool = new PoolConfig { MinSize = 2, MaxSize = 4, GatherStats = true }
        };

        var pool = new RabbitConnectionPool(config, loggerFactory);
        Assert.NotNull(pool);
        Assert.NotNull(pool.Stats);
        Assert.NotNull(pool.FullStatsReport); 
    }

    // [Fact]
    // public void PoolConfigOrDefaults_ReturnsDefaults_WhenNull()
    // {
    //     var method = typeof(RabbitConnectionPool).GetMethod("PoolConfigOrDefaults", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    //     var result = method?.Invoke(null, new object[] { null }) as PoolConfig;
    //     Assert.NotNull(result);
    //     Assert.Equal(1, result.MinSize);
    //     Assert.Equal(5, result.MaxSize);
    //     Assert.False(result.Initialize);
    //     Assert.False(result.GatherStats);
    //     Assert.Equal(TimeSpan.FromMilliseconds(100), result.DefaultWaitTimeout);
    // }

    [Fact]
    public void Dispose_CallsPoolDispose()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var config = new RabbitPublisherConfig { ClientName = "TestDispose" };
        var pool = new RabbitConnectionPool(config, loggerFactory);

        pool.Dispose();
        // Should not throw, and can call Dispose again
        pool.Dispose();
    }

    [Fact]
    public void BuildCreateConnectionErrorMessage_FormatsError()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var config = new RabbitPublisherConfig { ClientName = "TestError" };
        var pool = new RabbitConnectionPool(config, loggerFactory);

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "user",
            Password = "pass"
        };
        var endpoints = new List<AmqpTcpEndpoint> { new AmqpTcpEndpoint("localhost", 5672) };

        var method = typeof(RabbitConnectionPool).GetMethod("BuildCreateConnectionErrorMessage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var error = (string?)method?.Invoke(pool, [factory, endpoints]);
        Assert.Contains("localhost", error);
        Assert.Contains("user", error);
        Assert.Contains("pass", error);
    }

    [Fact]
    public async Task ReportHealthStatusAsync_DelegatesToPool()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var config = new RabbitPublisherConfig { ClientName = "TestHealth" };
        var pool = new RabbitConnectionPool(config, loggerFactory);

        var report = await pool.ReportHealthStatusAsync();
        Assert.NotNull(report);
    }
}