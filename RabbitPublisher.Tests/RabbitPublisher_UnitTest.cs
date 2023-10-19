// using Xunit;
// using FastCSharp.RabbitPublisher.Impl;
// using FastCSharp.RabbitCommon;
// using RabbitMQ.Client;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Configuration;
// using Moq;
// using Microsoft.Extensions.Primitives;
// using FastCSharp.RabbitPublisher.Common;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Options;
// using FastCSharp.RabbitPublisher.Injection;
// using FastCSharp.Publisher;

// namespace FastCSharp.RabbitPublisher.Tests;

// public class RabbitPublisher_UnitTest
// {
//     readonly ILoggerFactory loggerFactory;

//     readonly IConfiguration configuration;
//     readonly IConfiguration emptyConfiguration;
//     readonly IConfiguration missingTopicRoutingKeyConfiguration;

//     readonly List<AmqpTcpEndpoint> hosts = new()
//     {
//         new AmqpTcpEndpoint("localhost", 5672)
//     };

//     public RabbitPublisher_UnitTest()
//     {
//         loggerFactory = LoggerFactory.Create(builder =>
//         {
//             builder
//                 .AddFilter("Microsoft", LogLevel.Warning)
//                 .AddFilter("System", LogLevel.Warning)
//                 .AddFilter("NonHostConsoleApp.Program", LogLevel.Debug)
//                 .AddConsole();
//         });

//         emptyConfiguration = new ConfigurationBuilder()
//             .AddInMemoryCollection(new Dictionary<string, string?>
//                 {
//                 })
//             .Build();
//         missingTopicRoutingKeyConfiguration = new ConfigurationBuilder()
//             .AddInMemoryCollection(new Dictionary<string, string?>
//                 {
//                 })
//             .Build();
//         configuration = new ConfigurationBuilder()
//             .AddInMemoryCollection(new Dictionary<string, string?>
//                 {
//                     ["SecretKey"] = "Dictionary MyKey Value",
//                     ["TransientFaultHandlingOptions:Enabled"] = bool.TrueString,
//                     ["TransientFaultHandlingOptions:AutoRetryDelay"] = "00:00:07",
//                     ["Logging:LogLevel:Default"] = "Warning",

//                     ["RabbitPublisherConfig:ClientName"] = "FCS Test",
//                     ["RabbitPublisherConfig:VirtualHost"] = "MyVirtualHost",
//                     ["RabbitPublisherConfig:Password"] = "Password",
//                     ["RabbitPublisherConfig:UserName"] = "UserName",
//                     ["RabbitPublisherConfig:Timeout"] = "00:00:05",
//                     ["RabbitPublisherConfig:Hosts:0:HostName"] = "localhost",
//                     ["RabbitPublisherConfig:Hosts:0:Port"] = "5672",

//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.DIRECT:Name"] = "test.direct.exchange",
//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.DIRECT:Type"] = "Direct",
//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.DIRECT:Queues:TASK_QUEUE"] = "test.direct.q",

//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:Name"] = "test.topic.exchange",
//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:Type"] = "Topic",
//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:RoutingKeys:0"] = ".mail.",
//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:RoutingKeys:1"] = ".sms.",
//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.TOPIC:RoutingKeys:2"] = ".letter.",

//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.FANOUT:Name"] = "test.fanout.exchange",
//                     ["RabbitPublisherConfig:Exchanges:PUBLISH.SDK.FANOUT:Type"] = "Fanout",
//                 })
//             .Build();

//     }

//     [Fact]
//     public void CreateNewPublisherFactory()
//     {
//         using var exchange = new RabbitDirectPublisherFactory(configuration, loggerFactory);
//         Assert.NotNull(exchange);
//     }

//     [Fact]
//     public void CreateNewDirectPublisher()
//     {
//         using var exchange = new RabbitDirectPublisherFactory(configuration, loggerFactory);
//         {
//             using var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE");
//             Assert.NotNull(publisher);
//         }
//         GC.Collect(0, GCCollectionMode.Forced);
//         GC.WaitForPendingFinalizers();
//         Assert.NotNull(exchange);
//     }

//     [Fact]
//     public void TestRabbitConnection()
//     {
//         // Given
//         var connectionFactory = new Mock<IConnection>();
//         var loggerFactory = new Mock<ILoggerFactory>();
//         var conn = new RabbitConnection(connectionFactory.Object, loggerFactory.Object);
//         // When
//         Assert.False(conn.IsOpen);
//         // Then
//     }

//     [Fact]
//     public void FailToCreateNewDirectPublisher()
//     {
//         using var exchange = new RabbitDirectPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
//     }

//     [Fact]
//     public void FailToCreateNewDirectPublisherWithNullRoutingKey()
//     {
//         using var exchange = new RabbitDirectPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", null));
//     }

//     [Fact]
//     public void FailToCreateNewDirectPublisherWithDefaultRoutingKey()
//     {
//         using var exchange = new RabbitDirectPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT"));
//     }

//     [Fact]
//     public void CreateNewDirectPublisherWithoutFailedConfiguration()
//     {
//         using var exchange = new RabbitDirectPublisherFactory(emptyConfiguration, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
//     }

//     [Fact]
//     public void CreateTopicPublisherWithoutFailedConfiguration()
//     {
//         using var exchange = new RabbitTopicPublisherFactory(emptyConfiguration, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
//     }

//     [Fact]
//     public void FailCreateTopicPublisherWithoutExchangeConfiguration()
//     {
//         var configuration = new Mock<IConfiguration>();
//         var section = new Section(); 
//         var config = new Mock<RabbitPublisherConfig>();
//         configuration.Setup(c => c.GetSection(nameof(RabbitPublisherConfig)))
//             .Returns(section);
//         using var exchange = new RabbitTopicPublisherFactory(configuration.Object, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("FAIL.TO.GET.EXCHANGE", "TASK_QUEUE"));
//     }

//     [Fact]
//     public void CreateTopicPublisherWithMissingRoutingKey()
//     {
//         using var exchange = new RabbitTopicPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".snail."));
//     }

//     [Fact]
//     public void DirectPublisherWrongQueue()
//     {
//         using var exchange = new RabbitDirectPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "WRONG_QUEUE"));
//     }

//     [Fact]
//     public void CreateNewFanoutPublisher()
//     {
//         using var exchange = new RabbitFanoutPublisherFactory(configuration, loggerFactory);
//         using (var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT"))
//         {
//             Assert.NotNull(publisher);
//         };
//     }

//     [Fact]
//     public async void CreateNewFanoutPublisher_ConfirmDispose()
//     {
//         using var exchange = new RabbitFanoutPublisherFactory(configuration, loggerFactory);
//         IPublisher<string>? publisher = null;
//         using (publisher = exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT"))
//         {
//             Assert.NotNull(publisher);
//         };
//         GC.Collect();
//         await Assert.ThrowsAsync<ObjectDisposedException>(() => publisher.Publish(""));
//     }

//     [Fact]
//     public void CreateNewTopicPublisher()
//     {
//         using var exchange = new RabbitTopicPublisherFactory(configuration, loggerFactory);
//         using var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".mail.");
//         Assert.NotNull(publisher);
//         Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".sms."));
//         Assert.NotNull(exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".letter."));
//         publisher.Dispose();
//     }

//     [Fact]
//     public void TopicPublisherWrongKey()
//     {
//         using var exchange = new RabbitTopicPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<KeyNotFoundException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", "wrong.key"));
//     }

//     [Fact]
//     public void CreateWrongPublisherConfigurationTypeForFanout()
//     {
//         using var exchange = new RabbitFanoutPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC"));
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE"));
//     }

//     [Fact]
//     public void CreateWrongPublisherConfigurationTypeForDirect()
//     {
//         using var exchange = new RabbitDirectPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.TOPIC", ".mail."));
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT", "TASK_QUEUE"));
//     }

//     [Fact]
//     public void CreateWrongPublisherConfigurationTypeForTopic()
//     {
//         using var exchange = new RabbitTopicPublisherFactory(configuration, loggerFactory);
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.FANOUT", "TASK_QUEUE"));
//         Assert.Throws<ArgumentException>(() => exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE"));
//     }

//     [Fact]
//     public async void DirectExchange_Publish_Success()
//     {
//         var exchangeName = "TestExchange";
//         var routingKey = "TestQueue";
//         var mockedPool = new Mock<IRabbitConnectionPool>();

//         var mockedChannel = new Mock<IRabbitChannel>();
//         var mockedConnection = new Mock<IRabbitConnection>();
//         var mockedLoggerFactory = new Mock<ILoggerFactory>();

//         // var mockedRabbitPublisherConfiguration = new Mock<RabbitPublisherConfig>();

//         using var publisher = new DirectRabbitPublisher<string>(
//             mockedPool.Object,
//             loggerFactory,
//             exchangeName,
//             Timeout.InfiniteTimeSpan,
//             routingKey
//             );

//         mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, null, routingKey)).Returns(mockedChannel.Object);

//         mockedChannel.Setup(model => model.BasicPublish(publisher, It.IsAny<IBasicProperties>(), It.IsAny<byte[]>()))
//             .Verifiable("BasicPublish was not called");
//         mockedChannel.Setup(model => model.WaitForConfirmsOrDie(publisher, It.IsAny<TimeSpan>()))
//             .Verifiable("BasicPublish was not called");
            
//         mockedPool.Setup(conn => conn.Connection(publisher)).Returns(mockedConnection.Object);

//         Assert.True(await publisher.Publish("Test Message"));

//         mockedChannel.VerifyAll();
//     }

//     [Fact]
//     public async void DirectExchange_Publish_Fails()
//     {
//         var exchangeName = "TestExchange";
//         var routingKey = "TestQueue";

//         var mockedPool = new Mock<IRabbitConnectionPool>();
//         var mockedChannel = new Mock<IRabbitChannel>();
//         var mockedConnection = new Mock<IRabbitConnection>();

//         mockedChannel.Setup(model => 
//             model.BasicPublish(It.IsAny<object>(), 
//                 null, 
//                 It.IsAny<byte[]>())).Throws<Exception>();

//         using var publisher = new DirectRabbitPublisher<string>(
//             mockedPool.Object,
//             loggerFactory,
//             exchangeName,
//             Timeout.InfiniteTimeSpan,
//             routingKey
//             );

//         mockedPool.Setup(conn => conn.Connection(publisher)).Returns(mockedConnection.Object);
//         mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, null, routingKey)).Returns(mockedChannel.Object);

//         Assert.False(await publisher.Publish("Test Message"));

//         mockedPool.Verify(connection => connection.Connection(publisher), Times.AtLeastOnce());

//         mockedChannel.Verify(model => 
//             model.BasicPublish(
//                 It.IsAny<object>(), 
//                 null, 
//                 It.IsAny<byte[]>()), Times.AtLeastOnce());
                
//     }

//     [Fact]
//     public async void DirectExchange_FailToStartConnectionAndRecover()
//     {
//         var exchangeName = "TestExchange";
//         var routingKey = "TestQueue";

//         var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
//         var mockedConnection = new Mock<IRabbitConnection>();


//         var mockedChannel = new Mock<IRabbitChannel>();

//         using var publisher = new DirectRabbitPublisher<string>(
//             mockedConnectionPool.Object,
//             loggerFactory,
//             exchangeName,
//             Timeout.InfiniteTimeSpan,
//             routingKey
//             );

//         var sequence = new MockSequence();

//         mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Throws<Exception>();
//         mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Throws<Exception>();
//         mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Returns(mockedConnection.Object);

//         mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, null, routingKey)).Returns(mockedChannel.Object);

//         Assert.False(await publisher.Publish("Test Message"));
//         Assert.False(await publisher.Publish("Test Message"));
//         Assert.True(await publisher.Publish("Test Message"));

//         mockedChannel.Verify(model => 
//             model.BasicPublish(publisher, null, 
//                 It.IsAny<byte[]>()), Times.AtLeastOnce());
//     }

//     [Fact]
//     public async void FanoutExchange_FailToStartConnectionAndRecover()
//     {
//         var exchangeName = "TestExchange";
//         var routingKey = "";

//         var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
//         var mockedConnection = new Mock<IRabbitConnection>();


//         var mockedChannel = new Mock<IRabbitChannel>();

//         using var publisher = new FanoutRabbitPublisher<string>(
//             mockedConnectionPool.Object,
//             loggerFactory,
//             exchangeName,
//             Timeout.InfiniteTimeSpan
//             );

//         var sequence = new MockSequence();

//         mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Throws<Exception>();
//         mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Throws<Exception>();
//         mockedConnectionPool.InSequence(sequence).Setup(pool => pool.Connection(publisher)).Returns(mockedConnection.Object);

//         mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, null, routingKey)).Returns(mockedChannel.Object);

//         Assert.False(await publisher.Publish("Test Message"));
//         Assert.False(await publisher.Publish("Test Message"));
//         Assert.True(await publisher.Publish("Test Message"));

//         mockedConnectionPool.Verify(pool => pool.Connection(publisher), Times.AtLeastOnce());
//         mockedConnection.Verify(connection => connection.Channel(publisher, exchangeName, null, routingKey), Times.AtLeastOnce());

//         mockedChannel.Verify(model => 
//             model.BasicPublish(publisher, null, 
//                 It.IsAny<byte[]>()), Times.AtLeastOnce());
//     }

//     [Fact]
//     public async void DirectExchange_PublishRecoverThenOk()
//     {
//         var exchangeName = "TestExchange";
//         var routingKey = "TestQueue";

//         var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
//         var mockedConnection = new Mock<IRabbitConnection>();


//         var mockedChannel = new Mock<IRabbitChannel>();

//         using var publisher = new DirectRabbitPublisher<string>(
//             mockedConnectionPool.Object,
//             loggerFactory,
//             exchangeName,
//             Timeout.InfiniteTimeSpan,
//             routingKey
//             );

//         MockSequence sequence = new ();
//         mockedChannel.InSequence(sequence).Setup(model => 
//             model.BasicPublish(publisher, 
//                 null, 
//                 It.IsAny<byte[]>())).Throws<Exception>();

//         mockedChannel.InSequence(sequence).Setup(model => 
//             model.BasicPublish(publisher, 
//                 null, 
//                 It.IsAny<byte[]>()));

//         mockedConnectionPool.Setup(conn => conn.Connection(publisher)).Returns(mockedConnection.Object);
//         mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, null, routingKey)).Returns(mockedChannel.Object);

//         Assert.False(await publisher.Publish("Test Message"));
//         Assert.True(await publisher.Publish("Test Message"));

//         mockedChannel.Verify(model => 
//             model.BasicPublish(It.IsAny<object>(), 
//                 null, 
//                 It.IsAny<byte[]>()), Times.AtLeastOnce());
//     }

//     [Fact]
//     public async void DirectExchange_PublishFailsRecoverThenOk()
//     {
//         var exchangeName = "TestExchange";
//         var routingKey = "TestQueue";

//         var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
//         var mockedConnection = new Mock<IRabbitConnection>();
//         var mockedChannel = new Mock<IRabbitChannel>();

//         using var publisher = new DirectRabbitPublisher<string>(
//             mockedConnectionPool.Object,
//             loggerFactory,
//             exchangeName,
//             Timeout.InfiniteTimeSpan,
//             routingKey
//             );

//         mockedConnectionPool.Setup(pool => pool.Connection(publisher)).Returns(mockedConnection.Object);
//         mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, null, routingKey)).Returns(mockedChannel.Object);

//         var sequence = new MockSequence();

//         mockedChannel.InSequence(sequence).Setup(channel => channel.WaitForConfirmsOrDie(publisher, It.IsAny<TimeSpan>()) ).Throws<Exception>();

//         Assert.False(await publisher.Publish("Test Message"));
//         Assert.True(await publisher.Publish("Test Message"));

//         mockedConnectionPool.Verify(pool => pool.Connection(publisher), Times.AtLeastOnce());
//         mockedConnection.Verify(conn => conn.Channel(publisher, exchangeName, null, routingKey), Times.AtLeastOnce());

//         mockedChannel.Verify(model => 
//             model.BasicPublish(publisher, null, It.IsAny<byte[]>()), Times.AtLeastOnce());
//     }

//     [Fact]
//     public async void TopicExchange_PublishFailsRecoverThenOk()
//     {
//         var exchangeName = "TestExchange";
//         var routingKey = "TestQueue";

//         var mockedConnectionPool = new Mock<IRabbitConnectionPool>();
//         var mockedConnection = new Mock<IRabbitConnection>();
//         var mockedChannel = new Mock<IRabbitChannel>();

//         using var publisher = new TopicRabbitPublisher<string>(
//             mockedConnectionPool.Object,
//             loggerFactory,
//             exchangeName,
//             Timeout.InfiniteTimeSpan,
//             routingKey
//             );

//         mockedConnectionPool.Setup(pool => pool.Connection(publisher)).Returns(mockedConnection.Object);
//         mockedConnection.Setup(conn => conn.Channel(publisher, exchangeName, null, routingKey)).Returns(mockedChannel.Object);

//         var sequence = new MockSequence();
//         mockedChannel.InSequence(sequence).Setup(
//             channel => channel.WaitForConfirmsOrDie(It.IsAny<object>(), It.IsAny<TimeSpan>())
//             ).Throws<Exception>();

//         Assert.False(await publisher.Publish("Test Message"));
//         Assert.True(await publisher.Publish("Test Message"));

//         mockedConnection.Verify(connection => connection.Channel(publisher, exchangeName, null, routingKey), Times.AtLeastOnce());

//         mockedChannel.Verify(model => 
//             model.BasicPublish(It.IsAny<object>(), 
//                 null, 
//                 It.IsAny<byte[]>()), Times.AtLeastOnce());
//     }

//     [Fact]
//     public void FanoutExchange_UseDependencyInjection()
//     {
//         var serviceCollection = new ServiceCollection();
//         serviceCollection.AddRabbitPublisher(configuration);
        
//         serviceCollection.AddSingleton<ILoggerFactory>(new LoggerFactory());

//         var serviceProvider = serviceCollection.BuildServiceProvider();
//         var rabbitOptions = serviceProvider.GetRequiredService<IOptions<RabbitPublisherConfig>>();
//         Assert.NotNull(rabbitOptions);
//         Assert.NotNull(rabbitOptions.Value);
//         Assert.Equal("FCS Test", rabbitOptions.Value.ClientName);

//         using var serviceScope = serviceProvider.CreateScope();
//         var services = serviceScope.ServiceProvider;
//         Assert.NotNull(services);

//         Assert.Equal(typeof(FanoutPublisherFactory), services.GetRequiredService<IPublisherFactory<IFanoutPublisher>>().GetType());
//         Assert.Equal(typeof(TopicPublisherFactory), services.GetRequiredService<IPublisherFactory<ITopicPublisher>>().GetType());
//         Assert.Equal(typeof(DirectPublisherFactory), services.GetRequiredService<IPublisherFactory<IDirectPublisher>>().GetType());
//         Assert.Equal(typeof(FanoutBatchPublisherFactory), services.GetRequiredService<IBatchPublisherFactory<IFanoutPublisher>>().GetType());
//         Assert.Equal(typeof(TopicBatchPublisherFactory), services.GetRequiredService<IBatchPublisherFactory<ITopicPublisher>>().GetType());
//         Assert.Equal(typeof(DirectBatchPublisherFactory), services.GetRequiredService<IBatchPublisherFactory<IDirectPublisher>>().GetType());
//     }

//     [Fact]
//     public void FanoutExchange_UseDependencyInjectionConfigFromFile()
//     {
//         var serviceCollection = new ServiceCollection();
//         serviceCollection.AddRabbitPublisher("rabbitsettings.json");
        
//         serviceCollection.AddSingleton<ILoggerFactory>(new LoggerFactory());

//         var serviceProvider = serviceCollection.BuildServiceProvider();
//         var rabbitOptions = serviceProvider.GetRequiredService<IOptions<RabbitPublisherConfig>>();
//         Assert.NotNull(rabbitOptions);
//         Assert.NotNull(rabbitOptions.Value);
//         Assert.Equal("FastCSharp", rabbitOptions.Value.ClientName);

//         using var serviceScope = serviceProvider.CreateScope();
//         var services = serviceScope.ServiceProvider;
//         Assert.NotNull(services);

//         Assert.Equal(typeof(FanoutPublisherFactory), services.GetRequiredService<IPublisherFactory<IFanoutPublisher>>().GetType());
//         Assert.Equal(typeof(TopicPublisherFactory), services.GetRequiredService<IPublisherFactory<ITopicPublisher>>().GetType());
//         Assert.Equal(typeof(DirectPublisherFactory), services.GetRequiredService<IPublisherFactory<IDirectPublisher>>().GetType());
//         Assert.Equal(typeof(FanoutBatchPublisherFactory), services.GetRequiredService<IBatchPublisherFactory<IFanoutPublisher>>().GetType());
//         Assert.Equal(typeof(TopicBatchPublisherFactory), services.GetRequiredService<IBatchPublisherFactory<ITopicPublisher>>().GetType());
//         Assert.Equal(typeof(DirectBatchPublisherFactory), services.GetRequiredService<IBatchPublisherFactory<IDirectPublisher>>().GetType());
//     }

//     [Fact]
//     public void TestName()
//     {
//         // Given
//         var section = new Section();
//         // When

//         // Then
//         Assert.Throws<NotImplementedException>(() => section["a"]);
//         Assert.Throws<NotImplementedException>(() => section["a"] = "a");
//         Assert.Throws<NotImplementedException>(() => section.Key);
//         Assert.Throws<NotImplementedException>(() => section.Path);
//         section.Value = "a";
//         Assert.Throws<NotImplementedException>(section.GetReloadToken);
//         Assert.Throws<NotImplementedException>(() => section.GetSection("a"));
//     }
// }

// class Section : IConfigurationSection
// {
//     public string? this[string key] 
//     { 
//         get => throw new NotImplementedException(); 
//         set => throw new NotImplementedException(); 
//     }

//     public string Key => throw new NotImplementedException();

//     public string Path => throw new NotImplementedException();

//     public string? Value 
//     {   
//         get 
//         {
//             return null;
//         } 
//         set { } 
//     }

//     public IEnumerable<IConfigurationSection> GetChildren()
//     {
//         return new List<IConfigurationSection>();
//     }

//     public IChangeToken GetReloadToken()
//     {
//         throw new NotImplementedException();
//     }

//     public IConfigurationSection GetSection(string key)
//     {
//         throw new NotImplementedException();
//     }
// }
