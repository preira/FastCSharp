using Xunit;
using FastCSharp.RabbitPublisher.Impl;
using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using FastCSharp.RabbitPublisher.Common;

namespace FastCSharp.RabbitPublisher.Tests;

public class RabbitBatchPublisher_UnitTest
{
    readonly ILoggerFactory loggerFactory;

    readonly IConfiguration configuration;
    readonly IConfiguration emptyConfiguration;
    readonly IConfiguration missingTopicRoutingKeyConfiguration;

    readonly List<AmqpTcpEndpoint> hosts = new()
    {
        new AmqpTcpEndpoint("localhost", 5672)
    };

    public RabbitBatchPublisher_UnitTest()
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
        missingTopicRoutingKeyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                })
            .Build();
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SecretKey"] = "Dictionary MyKey Value",
                    ["TransientFaultHandlingOptions:Enabled"] = bool.TrueString,
                    ["TransientFaultHandlingOptions:AutoRetryDelay"] = "00:00:07",
                    ["Logging:LogLevel:Default"] = "Warning",

                    ["RabbitPublisherConfig:ClientName"] = "FCS Test",
                    ["RabbitPublisherConfig:VirtualHost"] = "MyVirtualHost",
                    ["RabbitPublisherConfig:Password"] = "Password",
                    ["RabbitPublisherConfig:UserName"] = "UserName",
                    ["RabbitPublisherConfig:Timeout"] = "00:00:05",
                    ["RabbitPublisherConfig:Hosts:0:HostName"] = "localhost",
                    ["RabbitPublisherConfig:Hosts:0:Port"] = "5672",

                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.DIRECT:Name"] = "test.direct.exchange",
                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.DIRECT:Type"] = "Direct",
                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.DIRECT:NamedRoutingKeys:TASK_QUEUE"] = "test.direct.q",

                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:Name"] = "test.topic.exchange",
                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:Type"] = "Topic",
                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:RoutingKeys:0"] = ".mail.",
                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:RoutingKeys:1"] = ".sms.",
                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:RoutingKeys:2"] = ".letter.",

                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.FANOUT:Name"] = "test.fanout.exchange",
                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.FANOUT:Type"] = "Fanout",
                })
            .Build();

    }

    [Fact]
    public void CreateNewPublisherFactory()
    {
        using var exchange = new RabbitDirectBatchPublisherFactory(configuration, loggerFactory);
        Assert.NotNull(exchange);
    }

    [Fact]
    public void CreateNewDirectPublisher()
    {
        using var exchange = new RabbitDirectBatchPublisherFactory(configuration, loggerFactory);
        {
            using var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE");
            Assert.NotNull(publisher);
        }
        GC.Collect(0, GCCollectionMode.Forced);
        GC.WaitForPendingFinalizers();
        Assert.NotNull(exchange);
    }

    [Fact]
    public void FailToCreateNewDirectPublisher()
    {
        using var exchange = new RabbitDirectBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
    }

    [Fact]
    public void FailToCreateNewDirectPublisherWithNullRoutingKey()
    {
        using var exchange = new RabbitDirectBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", null));
    }

    [Fact]
    public void FailToCreateNewDirectPublisherWithDefaultRoutingKey()
    {
        using var exchange = new RabbitDirectBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT"));
    }

    [Fact]
    public void CreateNewDirectPublisherWithoutFailedConfiguration()
    {
        using var exchange = new RabbitDirectBatchPublisherFactory(emptyConfiguration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
    }

    [Fact]
    public void CreateTopicPublisherWithoutFailedConfiguration()
    {
        using var exchange = new RabbitTopicBatchPublisherFactory(emptyConfiguration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
    }

    [Fact]
    public void FailCreateTopicPublisherWithoutExchangeConfiguration()
    {
        var configuration = new Mock<IConfiguration>();
        var section = new Section(); 
        var config = new Mock<RabbitPublisherConfig>();
        configuration.Setup(c => c.GetSection(nameof(RabbitPublisherConfig)))
            .Returns(section);
        using var exchange = new RabbitTopicBatchPublisherFactory(configuration.Object, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
    }

    [Fact]
    public void CreateTopicPublisherWithMissingRoutingKey()
    {
        using var exchange = new RabbitTopicBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".snail."));
    }

    [Fact]
    public void DirectPublisherWrongQueue()
    {
        using var exchange = new RabbitDirectBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "WRONG_QUEUE"));
    }

    [Fact]
    public void CreateNewFanoutPublisher()
    {
        using var exchange = new RabbitFanoutBatchPublisherFactory(configuration, loggerFactory);
        using var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT");
        Assert.NotNull(publisher);
        publisher.Dispose();
    }

    [Fact]
    public void CreateNewTopicPublisher()
    {
        using var exchange = new RabbitTopicBatchPublisherFactory(configuration, loggerFactory);
        using var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".mail.");
        Assert.NotNull(publisher);
        Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".sms."));
        Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".letter."));
        publisher.Dispose();
    }

    [Fact]
    public void TopicPublisherWrongKey()
    {
        using var exchange = new RabbitTopicBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", "wrong.key"));
    }

    [Fact]
    public void CreateWrongPublisherConfigurationTypeForFanout()
    {
        using var exchange = new RabbitFanoutBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC"));
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE"));
    }

    [Fact]
    public void CreateWrongPublisherConfigurationTypeForDirect()
    {
        using var exchange = new RabbitDirectBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".mail."));
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT", "TASK_QUEUE"));
    }

    [Fact]
    public void CreateWrongPublisherConfigurationTypeForTopic()
    {
        using var exchange = new RabbitTopicBatchPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT", "TASK_QUEUE"));
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE"));
    }

    [Fact]
    public async void DirectExchange_BatchPublish_Success()
    {
        var exchangeName = "TestExchange";
        var routingKey = "TestQueue";
        var mockedPool = new Mock<IRabbitConnectionPool>();

        var mockedChannel = new Mock<IRabbitChannel>();
        var mockedConnection = new Mock<IRabbitConnection>();
        var mockedLoggerFactory = new Mock<ILoggerFactory>();


        using var publisher = new DirectRabbitBatchPublisher<string>(
            mockedPool.Object,
            loggerFactory,
            exchangeName,
            Timeout.InfiniteTimeSpan,
            routingKey
            );

        mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, routingKey)).Returns(mockedChannel.Object);

        mockedChannel.Setup(model => model.BasicPublish(publisher, It.IsAny<IBasicProperties>(), It.IsAny<byte[]>()))
            .Verifiable("BasicPublish was not called");
        mockedChannel.Setup(model => model.WaitForConfirmsOrDie(publisher, It.IsAny<TimeSpan>()))
            .Verifiable("BasicPublish was not called");
            
        mockedPool.Setup(conn => conn.Connection(publisher)).Returns(mockedConnection.Object);

        Assert.True(await publisher.Publish(new string[] { "Test Message" }));	

        mockedChannel.VerifyAll();
    }

    [Fact]
    public async void DirectExchange_Publish_Fails()
    {
        var exchangeName = "TestExchange";
        var routingKey = "TestQueue";

        var mockedPool = new Mock<IRabbitConnectionPool>();
        var mockedChannel = new Mock<IRabbitChannel>();
        var mockedConnection = new Mock<IRabbitConnection>();

        mockedChannel.Setup(model => 
            model.BasicPublish(It.IsAny<object>(), 
                null, 
                It.IsAny<byte[]>())).Throws<Exception>();

        using var publisher = new DirectRabbitBatchPublisher<string>(
            mockedPool.Object,
            loggerFactory,
            exchangeName,
            Timeout.InfiniteTimeSpan,
            routingKey
            );

        mockedPool.Setup(conn => conn.Connection(publisher)).Returns(mockedConnection.Object);
        mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, routingKey)).Returns(mockedChannel.Object);

        Assert.False(await publisher.Publish(new string[]{"Test Message"}));

        mockedPool.Verify(connection => connection.Connection(publisher), Times.AtLeastOnce());

        mockedChannel.Verify(model => 
            model.BasicPublish(
                It.IsAny<object>(), 
                // "TestExchange", "TestQueue", 
                null, 
                It.IsAny<byte[]>()), Times.AtLeastOnce());

    }

    [Fact]
    public async void DirectExchange_FailToStartConnectionAndRecover()
    {
        var exchangeName = "TestExchange";
        var routingKey = "TestQueue";

        var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
        var mockedConnection = new Mock<IRabbitConnection>();


        var mockedChannel = new Mock<IRabbitChannel>();

        using var publisher = new DirectRabbitBatchPublisher<string>(
            mockedConnectionPool.Object,
            loggerFactory,
            exchangeName,
            Timeout.InfiniteTimeSpan,
            routingKey
            );

        var sequence = new MockSequence();

        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Throws<Exception>();
        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Throws<Exception>();
        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Returns(mockedConnection.Object);

        mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, routingKey)).Returns(mockedChannel.Object);

        Assert.False(await publisher.Publish(new string[]{"Test Message"}));
        Assert.False(await publisher.Publish(new string[]{"Test Message"}));
        Assert.True(await publisher.Publish(new string[]{"Test Message"}));

        mockedChannel.Verify(model => 
            model.BasicPublish(publisher, null, 
                It.IsAny<byte[]>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void FanoutExchange_FailToStartConnectionAndRecover()
    {
        var exchangeName = "TestExchange";
        var routingKey = "";

        var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
        var mockedConnection = new Mock<IRabbitConnection>();


        var mockedChannel = new Mock<IRabbitChannel>();

        using var publisher = new FanoutRabbitBatchPublisher<string>(
            mockedConnectionPool.Object,
            loggerFactory,
            exchangeName,
            Timeout.InfiniteTimeSpan
            );

        var sequence = new MockSequence();

        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Throws<Exception>();
        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Throws<Exception>();
        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Returns(mockedConnection.Object);

        mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, routingKey)).Returns(mockedChannel.Object);

        Assert.False(await publisher.Publish(new string[]{"Test Message", "Test Message", "Test Message"}));
        Assert.False(await publisher.Publish(new string[]{"Test Message", "Test Message", "Test Message"}));
        Assert.True(await publisher.Publish(new string[]{"Test Message", "Test Message", "Test Message"}));

        mockedConnectionPool.Verify(pool => pool.Connection(publisher), Times.AtLeastOnce());
        mockedConnection.Verify(connection => connection.Channel(publisher, exchangeName, routingKey), Times.AtLeastOnce());

        mockedChannel.Verify(model => 
            model.BasicPublish(publisher, null, 
                It.IsAny<byte[]>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_PublishRecoverThenOk()
    {
        var exchangeName = "TestExchange";
        var routingKey = "TestQueue";

        var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
        var mockedConnection = new Mock<IRabbitConnection>();


        var mockedChannel = new Mock<IRabbitChannel>();

        using var publisher = new DirectRabbitBatchPublisher<string>(
            mockedConnectionPool.Object,
            loggerFactory,
            exchangeName,
            Timeout.InfiniteTimeSpan,
            routingKey
            );

        MockSequence sequence = new ();
        mockedChannel.InSequence(sequence).Setup(model => 
            model.BasicPublish(publisher, 
                null, 
                It.IsAny<byte[]>())).Throws<Exception>();

        mockedChannel.InSequence(sequence).Setup(model => 
            model.BasicPublish(publisher, 
                null, 
                It.IsAny<byte[]>()));

        mockedConnectionPool.Setup(conn => conn.Connection(publisher)).Returns(mockedConnection.Object);
        mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, routingKey)).Returns(mockedChannel.Object);

        Assert.False(await publisher.Publish(new string[]{"Test Message"}));
        Assert.True(await publisher.Publish(new string[]{"Test Message"}));

        mockedChannel.Verify(model => 
            model.BasicPublish(It.IsAny<object>(), 
                null, 
                It.IsAny<byte[]>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_PublishFailsRecoverThenOk()
    {
        var exchangeName = "TestExchange";
        var routingKey = "TestQueue";

        var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
        var mockedConnection = new Mock<IRabbitConnection>();
        var mockedChannel = new Mock<IRabbitChannel>();

        using var publisher = new DirectRabbitBatchPublisher<string>(
            mockedConnectionPool.Object,
            loggerFactory,
            exchangeName,
            Timeout.InfiniteTimeSpan,
            routingKey
            );

        mockedConnectionPool.Setup(pool => pool.Connection(publisher)).Returns(mockedConnection.Object);
        mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, routingKey)).Returns(mockedChannel.Object);

        var sequence = new MockSequence();

        mockedChannel.InSequence(sequence).Setup(channel => channel.WaitForConfirmsOrDie(publisher, It.IsAny<TimeSpan>()) ).Throws<Exception>();

        Assert.False(await publisher.Publish(new string[]{"Test Message"}));
        Assert.True(await publisher.Publish(new string[]{"Test Message"}));

        mockedConnectionPool.Verify(pool => pool.Connection(publisher), Times.AtLeastOnce());
        mockedConnection.Verify(conn => conn.Channel(publisher, exchangeName, routingKey), Times.AtLeastOnce());

        mockedChannel.Verify(model => 
            model.BasicPublish(publisher, null, It.IsAny<byte[]>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void TopicExchange_PublishFailsRecoverThenOk()
    {
        var exchangeName = "TestExchange";
        var routingKey = "TestQueue";

        var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
        var mockedConnection = new Mock<IRabbitConnection>();
        var mockedChannel = new Mock<IRabbitChannel>();

        using var publisher = new TopicRabbitBatchPublisher<string>(
            mockedConnectionPool.Object,
            loggerFactory,
            exchangeName,
            Timeout.InfiniteTimeSpan,
            routingKey
            );

        mockedConnectionPool.Setup(pool => pool.Connection(publisher)).Returns(mockedConnection.Object);
        mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, routingKey)).Returns(mockedChannel.Object);

        var sequence = new MockSequence();
        mockedChannel.InSequence(sequence).Setup(
            channel => channel.WaitForConfirmsOrDie(It.IsAny<object>(), It.IsAny<TimeSpan>())
            ).Throws<Exception>();

        Assert.False(await publisher.Publish(new string[]{"Test Message"}));
        Assert.True(await publisher.Publish(new string[]{"Test Message"}));

        mockedConnection.Verify(connection => connection.Channel(publisher, exchangeName, routingKey), Times.AtLeastOnce());

        mockedChannel.Verify(model => 
            model.BasicPublish(It.IsAny<object>(), 
                null, 
                It.IsAny<byte[]>()), Times.AtLeastOnce());
    }
}

