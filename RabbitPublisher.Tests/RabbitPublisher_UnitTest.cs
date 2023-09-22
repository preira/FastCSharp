using Xunit;
using FastCSharp.RabbitPublisher.Impl;
using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Microsoft.Extensions.Primitives;

namespace FastCSharp.RabbitPublisher.Tests;

public class RabbitPublisher_UnitTest
{
    readonly ILoggerFactory loggerFactory;

    readonly IConfiguration configuration;
    readonly IConfiguration emptyConfiguration;
    readonly IConfiguration missingTopicRoutingKeyConfiguration;

    readonly List<AmqpTcpEndpoint> hosts = new()
    {
        new AmqpTcpEndpoint("localhost", 5672)
    };

    public RabbitPublisher_UnitTest()
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
        var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
        Assert.NotNull(exchange);
    }

    [Fact]
    public void CreateNewDirectPublisher()
    {
        var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
        {
            var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE");
            Assert.NotNull(publisher);
        }
        GC.Collect(0, GCCollectionMode.Forced);
        GC.WaitForPendingFinalizers();
        Assert.NotNull(exchange);
    }

    [Fact]
    public void FailToCreateNewDirectPublisher()
    {
        var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
    }

    [Fact]
    public void FailToCreateNewDirectPublisherWithNullRoutingKey()
    {
        var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", null));
    }

    [Fact]
    public void FailToCreateNewDirectPublisherWithDefaultRoutingKey()
    {
        var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT"));
    }

    [Fact]
    public void CreateNewDirectPublisherWithoutFailedConfiguration()
    {
        var exchange = new RabbitDirectExchangeFactory(emptyConfiguration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
    }

    [Fact]
    public void CreateTopicPublisherWithoutFailedConfiguration()
    {
        var exchange = new RabbitTopicExchangeFactory(emptyConfiguration, loggerFactory);
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
        var exchange = new RabbitTopicExchangeFactory(configuration.Object, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
    }

    [Fact]
    public void CreateTopicPublisherWithMissingRoutingKey()
    {
        var exchange = new RabbitTopicExchangeFactory(configuration, loggerFactory);
        Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".snail."));
    }

    [Fact]
    public void DirectPublisherWrongQueue()
    {
        var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
        Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "WRONG_QUEUE"));
    }

    [Fact]
    public void CreateNewFanoutPublisher()
    {
        var exchange = new RabbitFanoutExchangeFactory(configuration, loggerFactory);
        var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT");
        Assert.NotNull(publisher);
        publisher.Dispose();
    }

    [Fact]
    public void CreateNewTopicPublisher()
    {
        var exchange = new RabbitTopicExchangeFactory(configuration, loggerFactory);
        var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".mail.");
        Assert.NotNull(publisher);
        Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".sms."));
        Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".letter."));
        publisher.Dispose();
    }

    [Fact]
    public void TopicPublisherWrongKey()
    {
        var exchange = new RabbitTopicExchangeFactory(configuration, loggerFactory);
        Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", "wrong.key"));
    }

    [Fact]
    public void CreateWrongPublisherConfigurationTypeForFanout()
    {
        var exchange = new RabbitFanoutExchangeFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC"));
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE"));
    }

    [Fact]
    public void CreateWrongPublisherConfigurationTypeForDirect()
    {
        var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".mail."));
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT", "TASK_QUEUE"));
    }

    [Fact]
    public void CreateWrongPublisherConfigurationTypeForTopic()
    {
        var exchange = new RabbitTopicExchangeFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT", "TASK_QUEUE"));
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE"));
    }

    [Fact]
    public async void DirectExchange_Publish_Success()
    {
        var mockedFactory = new Mock<IFCSConnection>();

        var mockedModel = new Mock<IModel>();
        mockedFactory.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);

        using var publisher = new DirectRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.True(await publisher.Publish("Test Message"));

        mockedFactory.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_Publish_Fails()
    {
        var mockedFactory = new Mock<IFCSConnection>();

        var mockedModel = new Mock<IModel>();
        mockedFactory.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);
        mockedModel.Setup(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>())).Throws<Exception>();

        using var publisher = new DirectRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));

        mockedFactory.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_FailToStartConnectionAndRecover()
    {
        var mockedConnectionFactory = new Mock<IConnectionFactory>();
        var mockedConnection = new Mock<IConnection>();

        var sequence = new MockSequence();

        mockedConnectionFactory.InSequence(sequence).Setup(factory => factory.CreateConnection(hosts)).Throws<Exception>();
        mockedConnectionFactory.InSequence(sequence).Setup(factory => factory.CreateConnection(hosts)).Throws<Exception>();
        mockedConnectionFactory.InSequence(sequence).Setup(factory => factory.CreateConnection(hosts)).Returns(mockedConnection.Object);

        var factory = new RabbitConnection(mockedConnectionFactory.Object, loggerFactory, hosts);
        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);

        using var publisher = new DirectRabbitPublisher<string>(
            factory,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void FanoutExchange_FailToStartConnectionAndRecover()
    {
        var mockedConnectionFactory = new Mock<IConnectionFactory>();
        var mockedConnection = new Mock<IConnection>();

        var sequence = new MockSequence();

        mockedConnectionFactory.InSequence(sequence).Setup(factory => factory.CreateConnection(hosts)).Throws<Exception>();
        mockedConnectionFactory.InSequence(sequence).Setup(factory => factory.CreateConnection(hosts)).Throws<Exception>();
        mockedConnectionFactory.InSequence(sequence).Setup(factory => factory.CreateConnection(hosts)).Returns(mockedConnection.Object);

        var factory = new RabbitConnection(mockedConnectionFactory.Object, loggerFactory, hosts);
        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);

        using var publisher = new FanoutRabbitPublisher<string>(
            factory,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedConnectionFactory.Verify(factory => factory.CreateConnection(hosts), Times.AtLeastOnce());
        mockedConnection.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_PublishRecoverThenOk()
    {
        var mockedFactory = new Mock<IFCSConnection>();

        var mockedModel = new Mock<IModel>();
        mockedFactory.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);
        
        MockSequence sequence = new ();
        mockedModel.InSequence(sequence).Setup(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>())).Throws<Exception>();
        mockedModel.InSequence(sequence).Setup(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>()));

        using var publisher = new DirectRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedFactory.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_PublishFailsRecoverThenOk()
    {
        var mockedConnectionFactory = new Mock<IConnectionFactory>();

        var mockedConnection = new Mock<IConnection>();
        mockedConnectionFactory.Setup(conn => conn.CreateConnection(hosts)).Returns(mockedConnection.Object);

        var sequence = new MockSequence();

        var factory = new RabbitConnection(mockedConnectionFactory.Object, loggerFactory, hosts);
        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);

        mockedModel.InSequence(sequence).Setup(channel => channel.WaitForConfirmsOrDie(It.IsAny<TimeSpan>())).Throws<Exception>();
        mockedModel.InSequence(sequence).Setup(channel => channel.QueueDeclarePassive("TestQueue")).Throws<Exception>();
        mockedModel.InSequence(sequence).Setup(channel => channel.QueueDeclarePassive("TestQueue")).Throws<Exception>();

        using var publisher = new DirectRabbitPublisher<string>(
            factory,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedConnectionFactory.Verify(factory => factory.CreateConnection(hosts), Times.AtLeastOnce());
        mockedConnection.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void TopicExchange_PublishFailsRecoverThenOk()
    {
        var mockedFactory = new Mock<IFCSConnection>();

        var mockedModel = new Mock<IModel>();
        mockedFactory.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);

        var sequence = new MockSequence();
        mockedModel.InSequence(sequence).Setup(channel => channel.WaitForConfirmsOrDie(It.IsAny<TimeSpan>())).Throws<Exception>();

        using var publisher = new TopicRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedFactory.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<ReadOnlyMemory<byte>>()), Times.AtLeastOnce());
    }
}

class Section : IConfigurationSection
{
    public string? this[string key] 
    { 
        get => throw new NotImplementedException(); 
        set => throw new NotImplementedException(); 
    }

    public string Key => throw new NotImplementedException();

    public string Path => throw new NotImplementedException();

    public string? Value 
    {   
        get 
        {
            return null;
        } 
        set { } 
    }

    public IEnumerable<IConfigurationSection> GetChildren()
    {
        return new List<IConfigurationSection>();
    }

    public IChangeToken GetReloadToken()
    {
        throw new NotImplementedException();
    }

    public IConfigurationSection GetSection(string key)
    {
        throw new NotImplementedException();
    }
}
