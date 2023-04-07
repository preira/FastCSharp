using Xunit;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using FastCSharp.Exception;
using FastCSharp.Subscriber;
using FastCSharp.RabbitSubscriber.Impl;
using RabbitMQ.Client.Exceptions;

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
        ["RabbitSubscriberConfig:HostName"] = "localhost",
        ["RabbitSubscriberConfig:Port"] = "5672",
        ["RabbitSubscriberConfig:Password"] = "Password",
        ["RabbitSubscriberConfig:UserName"] = "UserName",
        ["RabbitSubscriberConfig:HeartbeatTimeout"] = "00:00:20",
    };
    Dictionary<string, string?> configQueue = new Dictionary<string, string?>
    {
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:Name"] = "queue.name",
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefecthCount"] = "1",
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefecthSize"] = "0",
    };
    Dictionary<string, string?> configBadQueue = new Dictionary<string, string?>
    {
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefecthCount"] = "1",
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefecthSize"] = "0",
    };
    Dictionary<string, string?> configBadQueue2 = new Dictionary<string, string?>
    {
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:Name"] = String.Empty,
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefecthCount"] = "1",
        ["RabbitSubscriberConfig:Queues:QUEUE_TOKEN:PrefecthSize"] = "0",
    };
    Dictionary<string, string?> configTaskQueue = new Dictionary<string, string?>
    {

        ["RabbitSubscriberConfig:Queues:TASK_QUEUE:Name"] = "test.direct.q",
        ["RabbitSubscriberConfig:Queues:TASK_QUEUE:PrefecthCount"] = "1",
        ["RabbitSubscriberConfig:Queues:TASK_QUEUE:PrefecthSize"] = "0",

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
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);

        using var subscriber = new RabbitSubscriber<object>(connectionFactory.Object, queue, loggerFactory);
        Assert.NotNull(subscriber);
    }

    [Fact]
    public void RabbitSubscriberFactory_Create()
    {
        var factory = new RabbitSubscriberFactory(configuration, loggerFactory);
        Assert.NotNull(factory);
        Assert.Throws<BrokerUnreachableException>(() => factory.NewSubscriber<object>("QUEUE_TOKEN"));
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
    public void RabbitSubscriber_Register()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            subscriber.Register(async (msg) => await new Task<bool>(() => true));
        }

        model.Verify(channel => channel.QueueDeclarePassive(queue.Name), Times.Once);
        model.Verify(channel => channel.BasicQos((uint)queue.PrefetchSize, (ushort)queue.PrefetchCount, false), Times.Once);
        model.Verify(channel => channel.QueueDeclare(queue.Name, true, false, false, null), Times.Never);
        model.Verify(channel => channel.Dispose(), Times.Once);
        connection.Verify(conn => conn.Dispose(), Times.Once);
    }

    [Fact]
    public async void RabbitSubscriber_TestConsumer()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            var basicProperties = new Mock<IBasicProperties>();
            basicProperties.SetupGet(prop => prop.MessageId).Returns("TestMessageId-1");
            var deliverEventArgs = new BasicDeliverEventArgs();
            deliverEventArgs.Body = JsonSerializer.SerializeToUtf8Bytes<string>("message");
            deliverEventArgs.DeliveryTag = 1;
            deliverEventArgs.BasicProperties = basicProperties.Object;

            await AsyncInvoke_GetListenerAndAwaitTaskCompletion(model, subscriber, deliverEventArgs, new Task<bool>(() => true));
            model.Verify(channel => channel.BasicAck(deliverEventArgs.DeliveryTag, false), Times.Once);

            await AsyncInvoke_GetListenerAndAwaitTaskCompletion(model, subscriber, deliverEventArgs, new Task<bool>(() => false));
            model.Verify(channel => channel.BasicNack(deliverEventArgs.DeliveryTag, false, true), Times.Once);

            await AsyncInvoke_GetListenerAndAwaitTaskCompletion(model, subscriber, deliverEventArgs, new Task<bool>(() => throw new JsonException()));
            model.Verify(channel => channel.BasicNack(deliverEventArgs.DeliveryTag, false, false), Times.Once);

            await AsyncInvoke_GetListenerAndAwaitTaskCompletion(model, subscriber, deliverEventArgs, new Task<bool>(() => throw new System.Exception()));
            model.Verify(channel => channel.BasicNack(deliverEventArgs.DeliveryTag, false, true), Times.Exactly(2));
        }
    }

    [Fact]
    public void RabbitSubscriber_TestConsumerJsonDeserializationFails()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            var basicProperties = new Mock<IBasicProperties>();
            basicProperties.SetupGet(prop => prop.MessageId).Returns("TestMessageId-1");
            var deliverEventArgs = new BasicDeliverEventArgs();
            deliverEventArgs.Body = new ReadOnlyMemory<byte>(new byte[5]);
            deliverEventArgs.DeliveryTag = 1;
            deliverEventArgs.BasicProperties = basicProperties.Object;

            var eventHandler = subscriber.GetListener(
                    AsyncBooleanTrue()
                );
            eventHandler(model, deliverEventArgs);
            model.Verify(channel => channel.BasicNack(deliverEventArgs.DeliveryTag, false, false), Times.Once);
        }
    }

    private static async Task AsyncInvoke_GetListenerAndAwaitTaskCompletion(Mock<IModel> model, RabbitSubscriber<string> subscriber, BasicDeliverEventArgs deliverEventArgs, Task<bool> task)
    {
        var eventHandler = subscriber.GetListener(
            async (msg) =>
            {
                task.Start();
                return await task;
            });
        eventHandler.Invoke(model, deliverEventArgs);

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
    public void Test_ResetClosedConnection()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            subscriber.Register(async (msg) => await new Task<bool>(() => true));
            connection.Setup(conn => conn.IsOpen).Returns(false);
            subscriber.ResetConnection();
            model.Verify(channel => channel.Dispose(), Times.Once);
            connection.Verify(conn => conn.Dispose(), Times.Once);
        }

        connection.Verify(conn => conn.CreateModel(), Times.Exactly(2));
        connectionFactory.Verify(factory => factory.CreateConnection(), Times.Exactly(2));
    }

    [Fact]
    public void Test_ResetOpenConnection()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            subscriber.Register(async (msg) => await new Task<bool>(() => true));
            connection.Setup(conn => conn.IsOpen).Returns(true);
            model.Setup(channel => channel.IsClosed).Returns(true);
            model.Verify(channel => channel.Dispose(), Times.Never);
            connection.Verify(conn => conn.Dispose(), Times.Never);
            connection.Verify(conn => conn.CreateModel(), Times.Once);
            connectionFactory.Verify(factory => factory.CreateConnection(), Times.Once);
            subscriber.ResetConnection();
            model.Verify(channel => channel.Dispose(), Times.Once);
            connection.Verify(conn => conn.Dispose(), Times.Never);
        }

        connection.Verify(conn => conn.CreateModel(), Times.Exactly(2));
        connectionFactory.Verify(factory => factory.CreateConnection(), Times.Once);
    }

    [Fact]
    public void Test_ResetAndRegisterConsumerWithNullCallback()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            Assert.Throws<IncorrectInitializationException>(() => subscriber.Reset());
        }
    }

    [Fact]
    public void Test_ResetOK()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            subscriber.Register(async (msg) => await new Task<bool>(() => true));
            subscriber.Reset();
        }
        model.Verify(channel => channel.QueueDeclarePassive(queue.Name), Times.Exactly(2));
    }

    [Fact]
    public void Test_UnSubscribe()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            subscriber.Register(async (msg) => await new Task<bool>(() => true));
            subscriber.UnSubscribe();
        }

        model.Verify(channel => channel.Dispose(), Times.Once);
        connection.Verify(conn => conn.Dispose(), Times.Once);
        connection.Verify(conn => conn.CreateModel(), Times.Once);
        connectionFactory.Verify(factory => factory.CreateConnection(), Times.Once);
    }

    [Fact]
    public void Test_CloseChannel()
    {
        var connectionFactory = new Mock<IConnectionFactory>();
        var queue = new RabbitQueueConfig()
        {
            Name = "queue.name",
            PrefetchCount = 1,
            PrefetchSize = 0,
        };
        var connection = new Mock<IConnection>();
        connectionFactory.Setup(factory => factory.CreateConnection()).Returns(connection.Object);
        var model = new Mock<IModel>();
        connection.Setup(conn => conn.CreateModel()).Returns(model.Object);

        using (var subscriber = new RabbitSubscriber<string>(connectionFactory.Object, queue, loggerFactory))
        {
            subscriber.Register(async (msg) => await new Task<bool>(() => true));
            subscriber.CloseChannel();
        }

        model.Verify(channel => channel.Close(It.IsAny<ushort>(), It.IsAny<string>()), Times.Once);
    }
}