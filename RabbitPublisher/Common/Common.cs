using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FastCSharp.Pool;
using System.Collections.Concurrent;

namespace FastCSharp.RabbitPublisher.Common;

public class ExchangeConfig
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public IDictionary<string, string?>? Queues { get; set; }
    public IList<string>? RoutingKeys { get; set; }
}

public class RabbitPublisherConfig : RabbitConfig
{
    public TimeSpan Timeout { get; set; }
    public Dictionary<string, ExchangeConfig?>? Exchanges { get; set; }
}

public class RabbitConnection : Individual<IConnection>, IRabbitConnection
{
    private readonly IConnection connection;
    public bool IsOpen => connection.IsOpen;
    private readonly ConcurrentDictionary<Tuple<string, string?, string?>, Pool<RabbitChannel, IModel>> channelsPools;
    readonly private ILogger logger;
    private readonly int minObjects;
    private readonly int maxObjects;
    private readonly int defaultTimeout;
    private readonly bool gatherStats = false;
    private readonly bool initialize = false;

    // TODO: pass min, max, defaultTimeout and gather stats from configuration
    public RabbitConnection(IConnection connection, ILoggerFactory ILoggerFactory)
    : base(connection)
    {
        this.connection = connection;
        logger = ILoggerFactory.CreateLogger<RabbitConnection>();
        channelsPools = new ();
        minObjects = 1;
        maxObjects = 10;
        defaultTimeout = 1000;
    }

    public IRabbitChannel Channel(object owner, string exchangeName, string? queue, string? routingKey = null)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);

        Pool<RabbitChannel, IModel>? pool;
        bool poolExists = channelsPools.TryGetValue(Tuple.Create(exchangeName, queue, routingKey), out pool);

        if(!poolExists)
        {
            // TODO: pass min, max, initialize defaultTimeout and gather stats from configuration
            // if it doesn't exist create a new pool after verifying the exchange and routing key
            pool = new Pool<RabbitChannel, IModel>(
                () => Create(exchangeName, queue, routingKey),
                minObjects, maxObjects, initialize, gatherStats, defaultTimeout
            );
            _ = Create(exchangeName, queue, routingKey);
            channelsPools.TryAdd(Tuple.Create(exchangeName, queue, routingKey), pool);
        }

        if(pool == null)
        {
            throw new Exception("FastCSharp could not create a new channel.");
        }

        return pool.Borrow(owner);
    }
    
    private RabbitChannel Create(string exchangeName, string? queue, string? routingKey, bool confims = true)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);
        var channel = connection?.CreateModel();
        if(channel == null)
        {
            throw new Exception("FastCSharp could not create a new channel.");
        }
        if(confims)
        {
            channel.ConfirmSelect();
        }
        return new RabbitChannel(channel, exchangeName, queue, routingKey);
    }

    public void Close() 
    {
        channelsPools.ToList().ForEach(e => e.Value.Dispose());
        channelsPools.Clear();
        if(connection?.IsOpen ?? false)
        {
            connection.Close();
        }
    } 

    public void DisposeValue() 
    {
        Close();
        connection?.Dispose();
        disposed = true;
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

public interface IRabbitConnectionPool : IDisposable
{
    IRabbitConnection Connection(object owner);

    IPoolStats? Stats { get; }
}


public class InjectableRabbitConnectionPool : RabbitConnectionPool
{
    public InjectableRabbitConnectionPool(IOptions<RabbitPublisherConfig> config, ILoggerFactory loggerFactory)
        : base(config.Value, loggerFactory)
    {
    }
}

public class RabbitConnectionPool : IRabbitConnectionPool
{
    private readonly string nameToken;
    static private int instanceCount = 0;
    readonly Pool<RabbitConnection, IConnection> pool;
    readonly ILogger logger;

    public IPoolStats? Stats => pool?.Stats;

    public RabbitConnectionPool(
        RabbitPublisherConfig config, 
        ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<RabbitConnectionPool>();

        nameToken = config.ClientName ?? "FastCSharp.RabbitPublisher";
        var factory = new ConnectionFactory();

        if(config.HostName != null)     factory.HostName = config.HostName;
        if(config.Port != null)         factory.Port = (int) config.Port;
        if(config.VirtualHost != null)  factory.VirtualHost = config.VirtualHost;
        if(config.Password != null)     factory.Password = config.Password;
        if(config.UserName != null)     factory.UserName = config.UserName;
        if(config.Heartbeat != null)    factory.RequestedHeartbeat = (TimeSpan) config.Heartbeat;

        var poolConfig = PoolConfigOrDefaults(config.Pool);

        pool = new Pool<RabbitConnection, IConnection>(
            () => CreateConnection(factory, config.Hosts, loggerFactory),
            poolConfig.MinSize, 
            poolConfig.MaxSize, 
            poolConfig.Initialize, 
            poolConfig.GatherStats, 
            poolConfig.DefaultWaitTimeout.TotalMilliseconds
        );
    }

    private string getName() => $"{nameToken}-{Interlocked.Increment(ref instanceCount).ToString("D6")}";

    static private PoolConfig PoolConfigOrDefaults(PoolConfig? fromConfig)
    {
        var poolConf = new PoolConfig
        {
            MinSize = fromConfig?.MinSize ?? 1,
            MaxSize = fromConfig?.MaxSize ?? 5,
            Initialize = fromConfig?.Initialize ?? false,
            GatherStats = fromConfig?.GatherStats ?? false,
            DefaultWaitTimeout = fromConfig?.DefaultWaitTimeout ?? TimeSpan.FromMilliseconds(100)
        };
        return poolConf;
    }

    private RabbitConnection CreateConnection(ConnectionFactory factory, IList<AmqpTcpEndpoint>? endpoints, ILoggerFactory loggerFactory)
    {
        IConnection? connection;
        try
        {
            factory.ClientProvidedName = getName();
            logger.LogInformation($"Created RabbitConnection #{instanceCount}");
            if(endpoints == null)
            {
                connection = factory.CreateConnection();
            }
            else
            {
                connection = factory.CreateConnection(endpoints);
            }

            return new RabbitConnection(connection, loggerFactory);

        }
        catch (Exception ex)
        {

            var error = $"\tFactory URI: {factory.Uri}\n";
            error += $"\tFactory endpoint: {factory?.HostName}:{factory?.Port}\n";

            if(endpoints != null)
            {
                var i = 0;
                endpoints.ToList().ForEach(e =>
                    error += $"\tendpoint {++i}: {e}\n"
                );
            }
            var pw = factory?.Password;
            string? staredPw = null;

            if (pw!=null)
            {
                staredPw = $"{pw[..1]}******{pw[^2..]}";
            }
            error += $"\nThis may be due to incorrect authentication. Please check your user ('{factory?.UserName}') and password ('{staredPw}').";
            logger.LogError("[CONFIG ERROR] {message}\n{error}\n", ex.Message, error);

            throw new Exception(error, ex);
        }
    }    

    public IRabbitConnection Connection(object owner)
    {
        return pool.Borrow(owner);
    }

    public void Dispose()
    {
        pool.Dispose();
    }
}