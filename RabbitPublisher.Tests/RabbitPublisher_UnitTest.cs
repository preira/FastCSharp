using Xunit;
using FastCSharp.RabbitPublisher.Impl;
using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Microsoft.Extensions.Primitives;
using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FastCSharp.Publisher;

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
                    ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.DIRECT:Queues:TASK_QUEUE"] = "test.direct.q",

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
    public void CreateNewDirectPublisher()
    {
        RabbitPublisherConfig config = GetRabbitConfig(configuration);

        using var publisher = new AsyncRabbitPublisher<string>(new RabbitConnectionPool(config, loggerFactory), loggerFactory, config);
        {
            var exchange = publisher.ForExchange("PUBLISH.SDK.DIRECT").ForQueue("TASK_QUEUE");
            Assert.NotNull(exchange);
        }

        GC.Collect(0, GCCollectionMode.Forced);
        GC.WaitForPendingFinalizers();
        Assert.NotNull(publisher);
    }

    private static RabbitPublisherConfig GetRabbitConfig(IConfiguration configuration)
    {
        var config = new RabbitPublisherConfig();
        var section = configuration.GetSection(nameof(RabbitPublisherConfig));
        section.Bind(config);
        return config;
    }

    [Fact]
    public void TestRabbitConnection()
    {
        // Given
        var connectionFactory = new Mock<IConnection>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var conn = new RabbitConnection(connectionFactory.Object, loggerFactory.Object);
        // When
        Assert.False(conn.IsOpen);
        // Then
    }

    [Fact]
    public void FailToCreateNewDirectPublisher()
    {
        RabbitPublisherConfig config = GetRabbitConfig(configuration);
        var conn = new RabbitConnectionPool(config, loggerFactory);
        
        using var publisher = new AsyncRabbitPublisher<string>(conn, loggerFactory, config);
        
        Assert.Throws<KeyNotFoundException>(() => publisher.ForExchange("FAIL.TO.GET.EXCHANGE").ForQueue("TASK_QUEUE"));
        
    }

    [Fact]
    public void FailToCreateNewDirectPublisherWithNullRoutingKey()
    {
        RabbitPublisherConfig config = GetRabbitConfig(configuration);
        
        var conn = new RabbitConnectionPool(config, loggerFactory);
        
        using var publisher = new AsyncRabbitPublisher<string>(conn, loggerFactory, config);
        
        Assert.Throws<ArgumentNullException>(() => publisher.ForExchange("PUBLISH.SDK.DIRECT").ForQueue(null!));
    }

    [Fact]
    public async Task FailToCreateNewDirectPublisherWithDefaultRoutingKey()
    {
        using AsyncRabbitPublisher<string> publisher = RabbitPublisherFactory(configuration, loggerFactory);
        var isPublished = await publisher.ForExchange("PUBLISH.SDK.DIRECT").PublishAsync("Test Message");
        Assert.False(isPublished);
    }

    private AsyncRabbitPublisher<string> RabbitPublisherFactory(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        RabbitPublisherConfig config = GetRabbitConfig(configuration);

        var conn = new RabbitConnectionPool(config, loggerFactory);
        var publisher = new AsyncRabbitPublisher<string>(conn, loggerFactory, config);
        return publisher;
    }

    [Fact]
    public void CreateNewDirectPublisherWithoutFailedConfiguration()
    {
        using var exchange = RabbitPublisherFactory(emptyConfiguration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.ForExchange("FAIL.TO.GET.EXCHANGE").ForQueue("TASK_QUEUE"));
    }

    [Fact]
    public void CreateTopicPublisherWithMissingRoutingKey()
    {
        using var exchange = RabbitPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.ForExchange("PUBLISH.SDK.TOPIC").ForRouting(".snail."));
    }

    [Fact]
    public void DirectPublisherWrongQueue()
    {
        using var exchange = RabbitPublisherFactory(configuration, loggerFactory);
        Assert.Throws<KeyNotFoundException>(() => exchange.ForExchange("PUBLISH.SDK.DIRECT").ForQueue("WRONG_QUEUE"));
    }

    [Fact]
    public void CreateNewFanoutPublisher()
    {
        using var exchange = RabbitPublisherFactory(configuration, loggerFactory);
        using (var publisher = exchange.ForExchange("PUBLISH.SDK.FANOUT"))
        {
            Assert.NotNull(publisher);
        };
    }

    [Fact]
    public async Task CreateNewFanoutPublisher_ConfirmDispose()
    {
        using var exchange = RabbitPublisherFactory(configuration, loggerFactory);
        IAsyncPublisher<string>? publisher = null;
        using (publisher = exchange.ForExchange("PUBLISH.SDK.FANOUT"))
        {
            Assert.NotNull(publisher);
        };
        GC.Collect();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => publisher.PublishAsync(""));
    }

    [Fact]
    public void CreateNewTopicPublisher()
    {
        using var exchange = RabbitPublisherFactory(configuration, loggerFactory);
        var publisher = exchange.ForExchange("PUBLISH.SDK.TOPIC").ForRouting(".mail.");
        Assert.NotNull(publisher);
        Assert.NotNull(exchange.ForExchange("PUBLISH.SDK.TOPIC").ForRouting(".sms."));
        Assert.NotNull(exchange.ForExchange("PUBLISH.SDK.TOPIC").ForRouting(".letter."));
        publisher.Dispose();
    }

    [Fact]
    public void TopicPublisherWrongKey()
    {
        using var exchange = RabbitPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.ForExchange("PUBLISH.SDK.TOPIC").ForRouting("wrong.key"));
    }

    [Fact]
    public void CreateWrongPublisherConfigurationTypeForFanout()
    {
        using var exchange = RabbitPublisherFactory(configuration, loggerFactory);
        Assert.Throws<ArgumentException>(() => exchange.ForExchange("PUBLISH.SDK.FANOUT").ForQueue("TASK_QUEUE"));
    }

    [Fact]
    public async Task DirectExchange_Publish_Success()
    {
        var exchangeName = "PUBLISH.SDK.DIRECT";
        var routingKey = "TASK_QUEUE";
        var mockedPool = new Mock<IRabbitConnectionPool>();

        var mockedChannel = new Mock<IRabbitChannel>();
        var mockedConnection = new Mock<IRabbitConnection>();
        var mockedLoggerFactory = new Mock<ILoggerFactory>();

        // var mockedRabbitPublisherConfiguration = new Mock<RabbitPublisherConfig>();

        using var publisher = new AsyncRabbitPublisher<string>(
            mockedPool.Object,
            loggerFactory,
            GetRabbitConfig(configuration)
            );

        mockedConnection.Setup(conn => conn.GetChannelAsync(publisher, "test.direct.exchange", "test.direct.q", null)).ReturnsAsync(mockedChannel.Object);

        mockedChannel.Setup(channel => channel.BasicPublishAsync(publisher, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Verifiable("BasicPublish was not called");
            
        mockedPool.Setup(conn => conn.GetConnectionAsync(publisher)).ReturnsAsync(mockedConnection.Object);

        Assert.True(await publisher.ForExchange(exchangeName).ForQueue(routingKey).PublishAsync("Test Message"));

        mockedChannel.VerifyAll();
    }

    [Fact]
    public async Task DirectExchange_BatchPublish_Success()
    {
        var exchangeName = "PUBLISH.SDK.DIRECT";
        var routingKey = "TASK_QUEUE";
        var mockedPool = new Mock<IRabbitConnectionPool>();

        var mockedChannel = new Mock<IRabbitChannel>();
        var mockedConnection = new Mock<IRabbitConnection>();
        var mockedLoggerFactory = new Mock<ILoggerFactory>();

        // var mockedRabbitPublisherConfiguration = new Mock<RabbitPublisherConfig>();

        using var publisher = new AsyncRabbitPublisher<string>(
            mockedPool.Object,
            loggerFactory,
            GetRabbitConfig(configuration)
            );

        mockedConnection.Setup(conn => conn.GetChannelAsync(publisher, "test.direct.exchange", "test.direct.q", null)).ReturnsAsync(mockedChannel.Object);

        mockedChannel.Setup(channel => channel.BasicPublishAsync(publisher, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Verifiable("BasicPublish was not called");
            
        mockedPool.Setup(conn => conn.GetConnectionAsync(publisher)).ReturnsAsync(mockedConnection.Object);

        Assert.True(await publisher.ForExchange(exchangeName).ForQueue(routingKey).PublishAsync(new List<string> { "Test Message1", "Test Message2" }));

        mockedChannel.VerifyAll();

        mockedChannel.Verify(channel => channel.BasicPublishAsync(publisher, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DirectExchange_Publish_Fails()
    {
        var exchangeName = "PUBLISH.SDK.DIRECT";
        var routingKey = "TASK_QUEUE";

        var mockedPool = new Mock<IRabbitConnectionPool>();
        var mockedChannel = new Mock<IRabbitChannel>();
        var mockedConnection = new Mock<IRabbitConnection>();

        using var publisher = new AsyncRabbitPublisher<string>(
            mockedPool.Object,
            loggerFactory,
            GetRabbitConfig(configuration)
            );
            
        mockedChannel.Setup(channel => channel.BasicPublishAsync(publisher, It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Test Exception"));

        mockedPool.Setup(conn => conn.GetConnectionAsync(publisher)).ReturnsAsync(mockedConnection.Object);
        mockedConnection.Setup(conn => conn.GetChannelAsync(publisher, "test.direct.exchange", "test.direct.q", null)).ReturnsAsync(mockedChannel.Object);

        Assert.False(await publisher.ForExchange(exchangeName).ForQueue(routingKey).PublishAsync("Test Message"));

        mockedPool.Verify(connection => connection.GetConnectionAsync(publisher), Times.AtLeastOnce());

    }

    [Fact]
    public async Task DirectExchange_FailToStartConnectionAndRecover()
    {
        var exchangeName = "PUBLISH.SDK.DIRECT";
        var routingKey = "TASK_QUEUE";

        var mockedConnectionPool = new Mock<IRabbitConnectionPool>();

        var mockedConnection = new Mock<IRabbitConnection>();
        var mockedChannel = new Mock<IRabbitChannel>();

        using var publisher = new AsyncRabbitPublisher<string>(
            mockedConnectionPool.Object,
            loggerFactory,
            GetRabbitConfig(configuration)
            );

        var sequence = new MockSequence();

        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.GetConnectionAsync(publisher)).Throws<Exception>();
        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.GetConnectionAsync(publisher)).Throws<Exception>();
        mockedConnectionPool.InSequence(sequence).Setup(pool => pool.GetConnectionAsync(publisher)).ReturnsAsync(mockedConnection.Object);

        mockedConnection.Setup(conn => conn.GetChannelAsync(publisher, "test.direct.exchange", "test.direct.q", null)).ReturnsAsync(mockedChannel.Object);

        publisher.ForExchange(exchangeName).ForQueue(routingKey);

        Assert.False(await publisher.PublishAsync("Test Message"));
        Assert.False(await publisher.PublishAsync("Test Message"));
        Assert.True(await publisher.PublishAsync("Test Message"));

        mockedChannel.Verify(channel => 
            channel.BasicPublishAsync(publisher, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }


    [Fact]
    public async Task DirectExchange_PublishFailsRecoverThenOk()
    {
        var exchangeName = "PUBLISH.SDK.DIRECT";
        var routingKey = "TASK_QUEUE";

        var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
        var mockedConnection = new Mock<IRabbitConnection>();
        var mockedChannel = new Mock<IRabbitChannel>();

        using var publisher = new AsyncRabbitPublisher<string>(
            mockedConnectionPool.Object,
            loggerFactory,
            GetRabbitConfig(configuration)
            );

        mockedConnectionPool.Setup(pool => pool.GetConnectionAsync(publisher)).ReturnsAsync(mockedConnection.Object);
        mockedConnection.Setup(conn => conn.GetChannelAsync(publisher, "test.direct.exchange", "test.direct.q", null)).ReturnsAsync(mockedChannel.Object);

        publisher.ForExchange(exchangeName).ForQueue(routingKey);

        var sequence = new MockSequence();

        mockedChannel
            .InSequence(sequence)
            .Setup(channel => channel.BasicPublishAsync(publisher, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        Assert.False(await publisher.PublishAsync("Test Message"));
        Assert.True(await publisher.PublishAsync("Test Message"));

        mockedConnectionPool.Verify(pool => pool.GetConnectionAsync(publisher), Times.AtLeastOnce());
        mockedConnection.Verify(conn => conn.GetChannelAsync(publisher, "test.direct.exchange", "test.direct.q", null), Times.AtLeastOnce());

        mockedChannel.Verify(channel => channel.BasicPublishAsync(publisher, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public void FanoutExchange_UseDependencyInjection()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILoggerFactory>(new LoggerFactory());
        serviceCollection.AddRabbitPublisher<string>(configuration);
        

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var rabbitOptions = serviceProvider.GetRequiredService<IOptions<RabbitPublisherConfig>>();
        Assert.NotNull(rabbitOptions);
        Assert.NotNull(rabbitOptions.Value);
        Assert.Equal("FCS Test", rabbitOptions.Value.ClientName);

        using var serviceScope = serviceProvider.CreateScope();
        var services = serviceScope.ServiceProvider;
        Assert.NotNull(services);

        Assert.Equal(typeof(AsyncRabbitPublisher<string>), services.GetRequiredService<IAsyncPublisher<string>>().GetType());
    }

    [Fact]
    public void FanoutExchange_UseDependencyInjectionConfigFromFile()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILoggerFactory>(new LoggerFactory());
        serviceCollection.AddRabbitPublisher<string>("rabbitsettings.json");
        

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var rabbitOptions = serviceProvider.GetRequiredService<IOptions<RabbitPublisherConfig>>();
        Assert.NotNull(rabbitOptions);
        Assert.NotNull(rabbitOptions.Value);
        Assert.Equal("FastCSharp", rabbitOptions.Value.ClientName);

        using var serviceScope = serviceProvider.CreateScope();
        var services = serviceScope.ServiceProvider;
        Assert.NotNull(services);

        Assert.Equal(typeof(AsyncRabbitPublisher<string>), services.GetRequiredService<IAsyncPublisher<string>>().GetType());
    }

    [Fact]
    public void TestName()
    {
        // Given
        var section = new Section();
        // When

        // Then
        Assert.Throws<NotImplementedException>(() => section["a"]);
        Assert.Throws<NotImplementedException>(() => section["a"] = "a");
        Assert.Throws<NotImplementedException>(() => section.Key);
        Assert.Throws<NotImplementedException>(() => section.Path);
        section.Value = "a";
        Assert.Throws<NotImplementedException>(section.GetReloadToken);
        Assert.Throws<NotImplementedException>(() => section.GetSection("a"));
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
