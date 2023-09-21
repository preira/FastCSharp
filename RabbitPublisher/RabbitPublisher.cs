using FastCSharp.Publisher;
using RabbitMQ.Client;
using FastCSharp.RabbitPublisher.Impl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FastCSharp.RabbitPublisher;

public class ExchangeConfig
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public IDictionary<string, string?>? NamedRoutingKeys { get; set; }
    public IList<string>? RoutingKeys { get; set; }
}
public class RabbitPublisherConfig
{
    public string? ClientName { get; set; }

    public string? hostName;
    public string? HostName { get => hostName ?? "localhost"; set => hostName = value; }

    private string? virtualHost;
    public string VirtualHost { get => virtualHost ?? "/"; set => virtualHost = value; }

    public int? port;
    public int Port { get => port ?? 5672; set => port = value; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public TimeSpan Timeout { get; set; }
    public Dictionary<string, ExchangeConfig?>? Exchanges { get; set; }

    private TimeSpan? heartBeat;
    public TimeSpan HeartBeat { get => heartBeat ?? TimeSpan.FromSeconds(60); set => heartBeat = value; }
}

// TODO: move to Publisher.cs ??
public interface IFCSConnection
{
    bool IsOpen { get; }
    IModel CreateModel();
    bool ResetConnection(bool dispose = true);
    void Close();
    void Dispose();
}

public class RabbitConnection : IFCSConnection
{
    private readonly IConnectionFactory factory;
    private IConnection? connection;
    private IList<IModel> channels;
    readonly private ILogger logger;
    public RabbitConnection(IConnectionFactory connectionFactory, ILoggerFactory ILoggerFactory)
    {
        logger = ILoggerFactory.CreateLogger<RabbitConnection>();
        factory = connectionFactory;

        channels = new List<IModel>();
    }
    public bool IsOpen => connection?.IsOpen ?? false;
    public IModel CreateModel()
    {
        if (connection == null || !connection.IsOpen)
        {
            ResetConnection();
        }
        var channel = connection?.CreateModel();
        if(channel == null)
        {
            throw new Exception("FastCSharp could not create a new channel.");
        }
        channels.Add(channel);
        return channel;
    }
    public void Close() 
    {
        foreach (var channel in channels)
        {
            channels.Remove(channel);
            channel.Close();
            channel.Dispose();
        }
        connection?.Close();

    } 
    public bool ResetConnection(bool dispose = true)
    {
        try
        {
            connection?.Dispose();

            connection = factory.CreateConnection();
        }
        catch (Exception ex)
        {
            if (dispose)
            {
                logger.LogError("[CONFIG ERROR] {message}", ex.Message);
            }
            else
            {
                logger.LogError("[INITIALIZATION ERROR] {messsage}", ex.Message);
            }
            logger.LogDebug("{stackTrace}", ex.GetType());
            logger.LogDebug("{stackTrace}", ex.StackTrace);

            return false;
        }
        return true;
    }    
    public void Dispose() 
    {
        foreach (var channel in channels)
        {
            channels.Remove(channel);
            channel.Dispose();
        }
        connection?.Dispose();
    } 
}
public abstract class AbstractRabbitExchangeFactory : IPublisherFactory
{
    protected RabbitPublisherConfig config = new();
    
    protected readonly IFCSConnection connectionFactory;
    protected ILoggerFactory ILoggerFactory;
    protected AbstractRabbitExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    {
        this.ILoggerFactory = ILoggerFactory;
        var section = configuration.GetSection(nameof(RabbitPublisherConfig));
        section.Bind(config);

        var factory = new ConnectionFactory
        {
            ClientProvidedName = config.ClientName ?? "FastCSharp.RabbitPublisher",
            HostName = config.HostName,
            VirtualHost = config.VirtualHost,
            Port = config.Port,
            Password = config.Password,
            UserName = config.UserName,
            RequestedHeartbeat = config.HeartBeat,
        };
        connectionFactory = new RabbitConnection(factory, ILoggerFactory);
    }
    protected ExchangeConfig _NewPublisher(string destination)
    {
        var exchange = config?.Exchanges?[destination];
        if (exchange == null || exchange.Name == null)
        {
            throw new ArgumentException($"Could not find the exchange for '{destination}' in the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }
        return exchange;
    }

    public abstract IPublisher<T> NewPublisher<T>(string destination, string? routingKey = null);
}

public class RabbitDirectExchangeFactory : AbstractRabbitExchangeFactory
{
    public RabbitDirectExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    { }
    public override IPublisher<T> NewPublisher<T>(string destination, string? routingKey = null)
    {
        if (routingKey == null)
        {
            throw new ArgumentException($"Cannot create a new Publisher without a Routing Key. Routing key is mandatory and should match the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your implementation and configuration.");
        }
        ExchangeConfig exchange = base._NewPublisher(destination);
        var key = exchange.NamedRoutingKeys?[routingKey];
        if (key == null)
        {
            throw new ArgumentException($"Could not find the routing key for '{routingKey}' in the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }

        string exchangeName = Util.SafelyExtractExchageName(exchange, "direct");
        return new DirectRabbitPublisher<T>(
                            factory: connectionFactory,
                            ILoggerFactory: ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout,
                            routingKey: key
                            );
    }
}

public class RabbitFanoutExchangeFactory : AbstractRabbitExchangeFactory
{
    public RabbitFanoutExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    public override IPublisher<T> NewPublisher<T>(string destination, string? routingKey = null)
    {
        ExchangeConfig exchange = base._NewPublisher(destination);
        string exchangeName = Util.SafelyExtractExchageName(exchange, "fanout");
        return new FanoutRabbitPublisher<T>(factory: connectionFactory,
                            ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout);
    }
}

public class RabbitTopicExchangeFactory : AbstractRabbitExchangeFactory
{
    public RabbitTopicExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    public override IPublisher<T> NewPublisher<T>(string destination, string? routingKey = null)
    {
        routingKey ??= "";
        ExchangeConfig exchange = base._NewPublisher(destination);
        string exchangeName = Util.SafelyExtractExchageName(exchange, "topic");
        var isOk = exchange?.RoutingKeys?.Contains(routingKey) ?? false;
        if (routingKey == "" || isOk)
        {
            return new TopicRabbitPublisher<T>(factory: connectionFactory,
                                ILoggerFactory,
                                exchange: exchangeName,
                                timeout: config.Timeout,
                                routingKey: routingKey);
        }
        throw new KeyNotFoundException($"Could not find the routing key for '{routingKey}' in RoutingKeys of the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
    }
}

internal static class Util
{
    internal static string SafelyExtractExchageName(ExchangeConfig exchange, string exchangeType)
    {
        if (exchange.Type?.ToLower() != exchangeType || exchange.Name == null)
        {
            throw new ArgumentException($"There is a problem in your configuration. You are trying to use a {exchangeType} with a {exchange.Type} configuration for exchange named '{exchange.Name}'.");
        }
        return exchange.Name;
    }
}
