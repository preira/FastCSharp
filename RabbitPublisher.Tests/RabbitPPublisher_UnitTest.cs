using Xunit;
using FastCSharp.RabbitPublisher.Impl;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;

namespace FastCSharp.RabbitPublisher.Tests;

public class RabbitPublisher_UnitTest
{
    ILoggerFactory loggerFactory;

    IConfiguration configuration;

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

        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SecretKey"] = "Dictionary MyKey Value",
                    ["TransientFaultHandlingOptions:Enabled"] = bool.TrueString,
                    ["TransientFaultHandlingOptions:AutoRetryDelay"] = "00:00:07",
                    ["Logging:LogLevel:Default"] = "Warning",

                    ["RabbitPublisherConfig:HostName"] = "localhost",
                    ["RabbitPublisherConfig:Port"] = "5672",
                    ["RabbitPublisherConfig:Password"] = "Password",
                    ["RabbitPublisherConfig:UserName"] = "UserName",
                    ["RabbitPublisherConfig:Timeout"] = "00:00:05",

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
        var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE");
        Assert.NotNull(publisher);
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
    }

    [Fact]
    public void CreateNewTopicPublisher()
    {
        var exchange = new RabbitTopicExchangeFactory(configuration, loggerFactory);
        var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC");
        Assert.NotNull(publisher);
        Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".mail."));
        Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".sms."));
        Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".letter."));
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
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC"));
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT"));
    }

    [Fact]
    public void CreateWrongPublisherConfigurationTypeForTopic()
    {
        var exchange = new RabbitTopicExchangeFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT"));
        Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE"));
    }

    [Fact]
    public async void DirectExchange_Publish_Success()
    {
        var mockedFactory = new Mock<IConnectionFactory>();
        var mockedConnection = new Mock<IConnection>();
        mockedFactory.Setup(factory => factory.CreateConnection()).Returns(mockedConnection.Object);

        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);

        var publisher = new DirectRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.True(await publisher.Publish("Test Message"));

        mockedFactory.Verify(factory => factory.CreateConnection(), Times.AtLeastOnce());
        mockedConnection.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_Publish_Fails()
    {
        var mockedFactory = new Mock<IConnectionFactory>();
        var mockedConnection = new Mock<IConnection>();
        mockedFactory.Setup(factory => factory.CreateConnection()).Returns(mockedConnection.Object);

        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);
        mockedModel.Setup(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>())).Throws<Exception>();

        var publisher = new DirectRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));

        mockedFactory.Verify(factory => factory.CreateConnection(), Times.AtLeastOnce());
        mockedConnection.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_FailToStartConnectionAndRecover()
    {
        var mockedFactory = new Mock<IConnectionFactory>();
        var mockedConnection = new Mock<IConnection>();
        var sequence = new MockSequence();
        mockedFactory.InSequence(sequence).Setup(factory => factory.CreateConnection()).Throws<Exception>();
        mockedFactory.InSequence(sequence).Setup(factory => factory.CreateConnection()).Throws<Exception>();
        mockedFactory.InSequence(sequence).Setup(factory => factory.CreateConnection()).Returns(mockedConnection.Object);

        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);

        var publisher = new DirectRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedFactory.Verify(factory => factory.CreateConnection(), Times.AtLeastOnce());
        // mockedConnection.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void FanoutExchange_FailToStartConnectionAndRecover()
    {
        var mockedFactory = new Mock<IConnectionFactory>();
        var mockedConnection = new Mock<IConnection>();
        var sequence = new MockSequence();
        mockedFactory.InSequence(sequence).Setup(factory => factory.CreateConnection()).Throws<Exception>();
        mockedFactory.InSequence(sequence).Setup(factory => factory.CreateConnection()).Throws<Exception>();
        mockedFactory.InSequence(sequence).Setup(factory => factory.CreateConnection()).Returns(mockedConnection.Object);

        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);

        var publisher = new FanoutRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedFactory.Verify(factory => factory.CreateConnection(), Times.AtLeastOnce());
        mockedConnection.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void DirectExchange_PublishFailsRecoverThenOk()
    {
        //TODO: start ok, publish, fail, recover
        var mockedFactory = new Mock<IConnectionFactory>();
        var mockedConnection = new Mock<IConnection>();
        mockedFactory.Setup(factory => factory.CreateConnection()).Returns(mockedConnection.Object);

        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);
        var sequence = new MockSequence();
        mockedModel.InSequence(sequence).Setup(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>())).Throws<Exception>();
        mockedModel.InSequence(sequence).Setup(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>()));

        var publisher = new DirectRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedFactory.Verify(factory => factory.CreateConnection(), Times.AtLeastOnce());
        mockedConnection.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>()), Times.AtLeastOnce());
    }

    [Fact]
    public async void TopicExchange_PublishFailsRecoverThenOk()
    {
        //TODO: start ok, publish, fail, recover
        var mockedFactory = new Mock<IConnectionFactory>();
        var mockedConnection = new Mock<IConnection>();
        mockedFactory.Setup(factory => factory.CreateConnection()).Returns(mockedConnection.Object);

        var mockedModel = new Mock<IModel>();
        mockedConnection.Setup(conn => conn.CreateModel()).Returns(mockedModel.Object);
        var sequence = new MockSequence();
        mockedModel.InSequence(sequence).Setup(channel => channel.WaitForConfirmsOrDie(It.IsAny<TimeSpan>())).Throws<Exception>();
        mockedModel.InSequence(sequence).Setup(channel => channel.QueueDeclarePassive("TestQueue")).Throws<Exception>();
        mockedModel.InSequence(sequence).Setup(channel => channel.QueueDeclarePassive("TestQueue")).Throws<Exception>();

        var publisher = new DirectRabbitPublisher<string>(
            mockedFactory.Object,
            loggerFactory,
            "TestExchange",
            Timeout.InfiniteTimeSpan,
            "TestQueue"
            );

        Assert.False(await publisher.Publish("Test Message"));
        Assert.False(await publisher.Publish("Test Message"));
        Assert.True(await publisher.Publish("Test Message"));

        mockedFactory.Verify(factory => factory.CreateConnection(), Times.AtLeastOnce());
        mockedConnection.Verify(connection => connection.CreateModel(), Times.AtLeastOnce());

        mockedModel.Verify(model => 
            model.BasicPublish("TestExchange", "TestQueue", false, null, 
                It.IsAny<System.ReadOnlyMemory<Byte>>()), Times.AtLeastOnce());
    }
}
