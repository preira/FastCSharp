using Xunit;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using FastCSharp.Exceptions;
using FastCSharp.Subscriber;
using FastCSharp.RabbitSubscriber.Impl;
using FastCSharp.Observability;

namespace FastCSharp.RabbitSubscriber.Test;

public class RabbitSubscriber_UnitTest
{
    ILoggerFactory loggerFactory;

    IConfiguration configuration;
    IConfiguration noQueueConfiguration;
    IConfiguration emptyConfiguration;
    IConfiguration badQueueConfiguration1;
    IConfiguration badQueueConfiguration2;

    Dictionary<string, string?> configGeneral = new Dictionary<string, string?>
    {
        ["SecretKey"] = "Dictionary MyKey Value",
        ["TransientFaultHandlingOptions:Enabled"] = bool.TrueString,
        ["TransientFaultHandlingOptions:AutoRetryDelay"] = "00:00:07",
        ["Logging:LogLevel:Default"] = "Warning",
    };
    Dictionary<string, string?> configRabbit = new Dictionary<string, string?>
    {
        ["RabbitPublisherConfig:ClientName"] = "FCS Test",
        ["RabbitSubscriberConfig:HostName"] = "localhost",
        ["RabbitPublisherConfig:VirtualHost"] = "MyVirtualHost",
        ["RabbitSubscriberConfig:Port"] = "5672",
        ["RabbitSubscriberConfig:Password"] = "Password",
        ["RabbitSubscriberConfig:UserName"] = "UserName",
        ["RabbitSubscriberConfig:HeartbeatTimeout"] = "00:00:20",
    };
    Dictionary<string, string?> configQueue = new Dictionary<string, string?>
    {
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:Name"] = "queue.name",
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefetchCount"] = "1",
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefetchSize"] = "0",
    };
    Dictionary<string, string?> configBadQueue = new Dictionary<string, string?>
    {
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefetchCount"] = "1",
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefetchSize"] = "0",
    };
    Dictionary<string, string?> configBadQueue2 = new Dictionary<string, string?>
    {
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:Name"] = String.Empty,
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefetchCount"] = "1",
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefetchSize"] = "0",
    };
    Dictionary<string, string?> configTaskQueue = new Dictionary<string, string?>
    {

        ["RabbitSubscriberConfig:Queues:TASK_QUEUE:Name"] = "test.direct.q",
        ["RabbitSubscriberConfig:Queues:TASK_QUEUE:PrefetchCount"] = "1",
        ["RabbitSubscriberConfig:Queues:TASK_QUEUE:PrefetchSize"] = "0",

    };

    public RabbitSubscriber_UnitTest()
    {
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("NonHostConsoleApp.Program", LogLevel.Debug)
                .AddConsole();
        });

        emptyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
            })
            .Build();

        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                configGeneral
                    .Concat(configRabbit)
                    .Concat(configQueue)
                    .Concat(configTaskQueue)
                    .ToDictionary(x => x.Key, x => x.Value))
            .Build();

        noQueueConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                configGeneral
                    .Concat(configRabbit)
                    .ToDictionary(x => x.Key, x => x.Value))
            .Build();

        badQueueConfiguration1 = new ConfigurationBuilder()
            .AddInMemoryCollection(
                configGeneral
                    .Concat(configRabbit)
                    .Concat(configBadQueue)
                    .ToDictionary(x => x.Key, x => x.Value))
            .Build();

        badQueueConfiguration2 = new ConfigurationBuilder()
            .AddInMemoryCollection(
                configGeneral
                    .Concat(configRabbit)
                    .Concat(configBadQueue2)
                    .ToDictionary(x => x.Key, x => x.Value))
            .Build();
    }

    [Fact]
    public void RabbitSubscriberFactory_New()
    {
        var factory = new RabbitSubscriberFactory(configuration, loggerFactory);
        Assert.NotNull(factory);
    }

    [Fact]
    public void RabbitSubscriberFactory_FailConnectionConfig()
    {
        Assert.Throws<IncorrectInitializationException>(() => new RabbitSubscriberFactory(emptyConfiguration, loggerFactory));
    }

    [Fact]
    public void RabbitSubscriberFactory_FailQueueConfig()
    {
        Assert.Throws<IncorrectInitializationException>(() => new RabbitSubscriberFactory(noQueueConfiguration, loggerFactory));
    }

    [Fact]
    public void RabbitSubscriberFactory_WrongQueueConfig()
    {
        var rabbitsFactory = new RabbitSubscriberFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => rabbitsFactory.NewSubscriber<object>("WRONG_QUEUE"));
    }

    [Fact]
    public void RabbitSubscriberFactory_BadQueueConfig()
    {
        var rabbitsFactory = new RabbitSubscriberFactory(badQueueConfiguration1, loggerFactory);
        Assert.Throws<ArgumentException>(() => rabbitsFactory.NewSubscriber<object>("ANY_QUEUE"));
    }

    [Fact]
    public void RabbitSubscriberFactory_BadQueueConfig2()
    {
        var rabbitsFactory = new RabbitSubscriberFactory(badQueueConfiguration2, loggerFactory);
        Assert.Throws<ArgumentException>(() => rabbitsFactory.NewSubscriber<object>("ANY_QUEUE"));
    }

    [Fact]
    public void RabbitSubscriberFactory_NewSubscriber()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();

        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        using var subscriber = new RabbitSubscriber<object>(connectionFactory.Object, queue, loggerFactory, null);
        Assert.NotNull(subscriber);
    }

    [Fact]
    public void RabbitSubscriberFactory_FailCreateWithEmptyQueueName()
    {
        var factory = new RabbitSubscriberFactory(badQueueConfiguration2, loggerFactory);
        Assert.NotNull(factory);
        Assert.Throws<ArgumentException>(() => factory.NewSubscriber<object>("QUEUE_TOKEN"));
    }

    [Fact]
    public void RabbitSubscriberFactory_CreateWithNullQueueName()
    {
        var factory = new RabbitSubscriberFactory(badQueueConfiguration1, loggerFactory);
        Assert.NotNull(factory);
        Assert.Throws<ArgumentException>(() => factory.NewSubscriber<object>("QUEUE_TOKEN"));
    }


    [Fact]
    public async Task RabbitSubscriber_Register()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        var channel = new Mock<IChannel>();
        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);
        connection.Setup(conn => conn.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.IsOpen).Returns(true);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            // connection.IsOpen && channel.IsOpen
            await subscriber.RegisterAsync(async (msg) => await new Task<bool>(() => true));

            channel.Verify(channel => channel.QueueDeclarePassiveAsync(queue.Name, It.IsAny<CancellationToken>()), Times.Once);
            channel.Verify(channel => channel.BasicQosAsync((uint)queue.PrefetchSize, (ushort)queue.PrefetchCount, false, It.IsAny<CancellationToken>()), Times.Once);
            channel.Verify(channel => channel.Dispose(), Times.Once);
            connection.Verify(conn => conn.Dispose(), Times.Once);

            Assert.NotNull(subscriber.Options);

            Assert.True(subscriber.IsHealthy);
        }

    }

    [Fact]
    public async Task RabbitSubscriber_TestConsumer()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);
        var channel = new Mock<IChannel>();
        channel.Setup(_channel => _channel.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), CancellationToken.None));

        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);
        connection.Setup(conn => conn.IsOpen).Returns(true);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            await subscriber.RegisterAsync(async (msg) => await new Task<bool>(() => true));
            var basicProperties = new Mock<IBasicProperties>();
            basicProperties.SetupGet(prop => prop.MessageId).Returns("TestMessageId-1");
            ulong deliveryTag = 1;

            var deliverEventArgs = new BasicDeliverEventArgs(
                    Guid.NewGuid().ToString(),
                    deliveryTag,
                    false,
                    "exchange",
                    "routingKey",
                    basicProperties.Object,
                    JsonSerializer.SerializeToUtf8Bytes("message")
                );

            await AsyncInvoke_GetListenerAndAwaitTaskCompletion(channel, subscriber, deliverEventArgs, new Task<bool>(() => true));
            channel.Verify(channel => channel.BasicAckAsync(deliveryTag, false, It.IsAny<CancellationToken>()), Times.Once);

            await AsyncInvoke_GetListenerAndAwaitTaskCompletion(channel, subscriber, deliverEventArgs, new Task<bool>(() => false));
            channel.Verify(channel => channel.BasicNackAsync(deliveryTag, false, true, It.IsAny<CancellationToken>()), Times.Once);

            await AsyncInvoke_GetListenerAndAwaitTaskCompletion(channel, subscriber, deliverEventArgs, new Task<bool>(() => throw new JsonException()));
            channel.Verify(channel => channel.BasicNackAsync(deliveryTag, false, false, It.IsAny<CancellationToken>()), Times.Once);

            await AsyncInvoke_GetListenerAndAwaitTaskCompletion(channel, subscriber, deliverEventArgs, new Task<bool>(() => throw new Exception()));
            channel.Verify(channel => channel.BasicNackAsync(deliveryTag, false, true, It.IsAny<CancellationToken>()), Times.Exactly(2));

            var report = await subscriber.ReportHealthStatusAsync();
            Assert.NotNull(report);
            Assert.Equal(HealthStatus.Healthy, report.Status);
            Assert.NotNull(report.Summarize());
        }
    }

    [Fact]
    public async Task RabbitSubscriber_TestConsumerJsonDeserializationFails()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);
        var channel = new Mock<IChannel>();

        channel.Setup(_channel => _channel.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));

        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);

        connection.Setup(conn => conn.IsOpen).Returns(true);



        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            await subscriber.RegisterAsync(async (msg) => await new Task<bool>(() => true));
            var basicProperties = new Mock<IBasicProperties>();
            basicProperties.SetupGet(prop => prop.MessageId).Returns("TestMessageId-1");
            var deliverEventArgs = new BasicDeliverEventArgs(
                    Guid.NewGuid().ToString(), 
                    1, 
                    false, 
                    "exchange", 
                    "routingKey", 
                    basicProperties.Object,
                    new ReadOnlyMemory<byte>(new byte[5])
                );

            var eventHandlerAsync = subscriber.GetAsyncListener(
                    AsyncBooleanTrue()
                );
            await eventHandlerAsync(channel, deliverEventArgs);
            channel.Verify(channel => channel.BasicNackAsync(deliverEventArgs.DeliveryTag, false, false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    private static async Task AsyncInvoke_GetListenerAndAwaitTaskCompletion(
        Mock<IChannel> channel, RabbitSubscriber<string> subscriber, BasicDeliverEventArgs deliverEventArgs, Task<bool> task)
    {
        var eventHandler = subscriber.GetAsyncListener(
            async (msg) =>
            {
                task.Start();
                return await task;
            });
        await eventHandler.Invoke(channel, deliverEventArgs);

        while (!task.IsCompleted)
        {
            await Task.Delay(1);
        }
    }

    private static OnMessageCallback<string> AsyncBooleanTrue()
    {
        return async (msg) =>
        {
            var task = new Task<bool>(() => true);
            task.Start();
            return await task;
        };
    }

    [Fact]
    public async Task Test_ResetClosedConnection()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        var channel = new Mock<IChannel>();
        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);
        connection.Setup(conn => conn.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.IsOpen).Returns(true);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            await subscriber.RegisterAsync(async (msg) => await new Task<bool>(() => true));
            connection.Setup(conn => conn.IsOpen).Returns(false);
            
            // Loops until the connection is recovered
            var resetTask = subscriber.ResetConnectionAsync();

            // this is time sensitive because we need to ensure the reset task is running and has already failed.
            await Task.Delay(500);

            connection.Setup(conn => conn.IsOpen).Returns(true);

            // Wait for the reset task to complete
            await resetTask;

            channel.Verify(channel => channel.Dispose(), Times.AtLeastOnce);
            connection.Verify(conn => conn.Dispose(), Times.AtLeastOnce);
        }

        connection.Verify(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        connectionFactory.Verify(factory => factory.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

    }

    [Fact]
    public async Task Test_ResetOpenConnection()
    {
        int delay = 100;
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        var channel = new Mock<IChannel>();
        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);

        connection.Setup(conn => conn.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.IsOpen).Returns(true);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            await subscriber.RegisterAsync(async (msg) => await new Task<bool>(() => true));
            channel.Verify(channel => channel.Dispose(), Times.Never);
            connection.Verify(conn => conn.Dispose(), Times.Never);
            
            connection.Verify(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>()), Times.Once);
            connectionFactory.Verify(factory => factory.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            await Task.Delay(delay);
            channel.Setup(_channel => _channel.IsClosed).Returns(true);
            channel.Setup(_channel => _channel.IsOpen).Returns(false);
            await Task.Delay(delay);
            
            var resetTask = Task.Run(() => subscriber.ResetConnectionAsync());
            await Task.Delay(delay);
            channel.Setup(_channel => _channel.IsClosed).Returns(false);
            channel.Setup(_channel => _channel.IsOpen).Returns(true);
            await resetTask;

            channel.Verify(channel => channel.Dispose(), Times.AtLeastOnce);
            connection.Verify(conn => conn.Dispose(), Times.Never);
        }

        connection.Verify(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        connectionFactory.Verify(factory => factory.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Test_IsHealthyHandleChannelException()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        var channel = new Mock<IChannel>();
        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);

        connection.Setup(conn => conn.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.IsOpen).Returns(true);
        channel.Setup(_channel =>
            _channel
                .QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Queue not found"));

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            Assert.False(subscriber.IsHealthy);
        }
    }

    [Fact]
    public async Task Test_ResetAndRegisterConsumerWithNullCallback()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        var channel = new Mock<IChannel>();
        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);

        connection.Setup(conn => conn.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.IsOpen).Returns(true);
        // channel.Setup(_channel =>
        //     _channel
        //         .QueueDeclarePassiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        //         .ThrowsAsync(new Exception("Queue not found"));

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            await Assert.ThrowsAsync<IncorrectInitializationException>(async () => await subscriber.ResetAsync());
            Assert.False(subscriber.IsHealthy);
        }
    }

    [Fact]
    public async Task Test_ResetOK()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        var channel = new Mock<IChannel>();
        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);

        connection.Setup(conn => conn.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.IsOpen).Returns(true);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            await subscriber.RegisterAsync(async (msg) => await new Task<bool>(() => true));
            await subscriber.ResetAsync();
        }
        channel.Verify(channel => channel.QueueDeclarePassiveAsync(queue.Name, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Test_UnSubscribe()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        var channel = new Mock<IChannel>();
        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);

        connection.Setup(conn => conn.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.IsOpen).Returns(true);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            await subscriber.RegisterAsync(async (msg) => await new Task<bool>(() => true));
            await subscriber.UnSubscribeAsync();
        }

        channel.Verify(channel => channel.Dispose(), Times.Once);
        connection.Verify(conn => conn.Dispose(), Times.Once);
        connection.Verify(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        connectionFactory.Verify(factory => factory.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_CloseChannel()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new QueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        var channel = new Mock<IChannel>();
        
        connection.Setup(conn => conn.CreateChannelAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);

        connection.Setup(conn => conn.IsOpen).Returns(true);
        channel.Setup(_channel => _channel.IsOpen).Returns(true);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory, null))
        {
            await subscriber.RegisterAsync(async (msg) => await new Task<bool>(() => true));
            await subscriber.CloseChannel();
            Assert.NotNull(subscriber);
        }

    }
}