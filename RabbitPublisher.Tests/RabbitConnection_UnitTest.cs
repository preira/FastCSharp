using Moq;
using Xunit;
using RabbitMQ.Client;
using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.Logging;

namespace RabbitPublisher.Tests;

public class RabbitConnection_UnitTest
{
    [Fact]
    public async Task Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger<RabbitConnection>>();
        loggerFactoryMock.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        var connectionMock = new Mock<IConnection>();

        var channelMock = new Mock<IChannel>();
        // FIXME: Ensure that the channel mock is set up correctly
        connectionMock.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        // Act
        var connection = new RabbitConnection(
            connectionMock.Object,
            loggerFactoryMock.Object, 
            new FastCSharp.Pool.PoolConfig
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
    }

//     [Fact]
//     public void Connect_ShouldReturnOpenConnection()
//     {
//         // Arrange
//         var connectionMock = new Mock<IConnection>();
//         var loggerFactoryMock = new Mock<ILoggerFactory>();

//         // Act
//         var connection = new RabbitConnection(connectionMock.Object, loggerFactoryMock.Object);

//         // Assert
//         Assert.NotNull(connection);
//     }

//     [Fact]
//     public void Dispose_ShouldCloseConnection()
//     {
//         // Arrange
//         var connectionMock = new Mock<IConnection>();
//         var loggerFactoryMock = new Mock<ILoggerFactory>();

//         // Act
//         var connection = new RabbitConnection(connectionMock.Object, loggerFactoryMock.Object);

//         connection.Dispose();

//         // Assert
//         connectionMock.Verify(c => c.Close(), Times.Once);
//     }

//     [Fact]
//     public void Connect_WhenConnectionFails_ShouldThrowException()
//     {
//         // Arrange
//         var connectionMock = new Mock<IConnection>();
//         var loggerFactoryMock = new Mock<ILoggerFactory>();

//         // Act
//         var connection = new RabbitConnection(connectionMock.Object, loggerFactoryMock.Object);

//         // Act & Assert
//         Assert.Throws<Exception>(() => connection.Connect());
//     }
}