using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FastCSharp.SDK.Publisher;
using Microsoft.Extensions.Options;
using FastCSharp.Pool;
using System.Collections.Concurrent;

namespace FastCSharp.RabbitPublisher.Common;

public class ExchangeConfig
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public IDictionary<string, string?>? NamedRoutingKeys { get; set; }
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
    private readonly ConcurrentDictionary<Tuple<string, string>, Pool<RabbitChannel, IModel>> channelsPool;
    readonly private ILogger logger;

    public RabbitConnection(IConnection connection, ILoggerFactory ILoggerFactory)
    : base(connection)
    {
        this.connection = connection;
        logger = ILoggerFactory.CreateLogger<RabbitConnection>();
        channelsPool = new ();
    }

    public IRabbitChannel Channel(object owner, string exchangeName, string routingKey)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);

        Pool<RabbitChannel, IModel>? pool;
        bool poolExists = channelsPool.TryGetValue(Tuple.Create(exchangeName, routingKey), out pool);

        if(!poolExists)
        {
            // if it doesn't exist create a new pool after verifying the exchange and routing key
            pool = new Pool<RabbitChannel, IModel>(
                () => Create(exchangeName, routingKey),
                1, 5, true
            );
            _ = Create(exchangeName, routingKey);
            channelsPool.TryAdd(Tuple.Create(exchangeName, routingKey), pool);
        }

        if(pool == null)
        {
            throw new Exception("FastCSharp could not create a new channel.");
        }

        return pool.Borrow(owner);
    }
    private RabbitChannel Create(string exchangeName, string routingKey, bool confims = true)
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
        return new RabbitChannel(channel, exchangeName, routingKey);
    }

    public void Close() 
    {
        channelsPool.ToList().ForEach(e => e.Value.Dispose());
        channelsPool.Clear();
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

public abstract class AbstractRabbitExchangeFactory : IDisposable
{
    protected RabbitPublisherConfig config = new();
    
    protected readonly IRabbitConnectionPool connectionPool;
    protected ILoggerFactory loggerFactory;
    protected bool disposed = false;

    protected AbstractRabbitExchangeFactory(
        IOptions<RabbitPublisherConfig> options, 
        ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        config = options.Value;
        connectionPool = CreateRabbitConnectionPool(config, loggerFactory);
    }

    protected AbstractRabbitExchangeFactory(
        IConfiguration configuration, 
        ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        var section = configuration.GetSection(nameof(RabbitPublisherConfig));
        section.Bind(config);
        
        connectionPool = CreateRabbitConnectionPool(config, loggerFactory);
    }

    private static RabbitConnectionPool CreateRabbitConnectionPool(RabbitPublisherConfig config, ILoggerFactory loggerFactory)
    {
        
        return new RabbitConnectionPool(config, loggerFactory);
    }

    protected ExchangeConfig GetExchangeConfig(string destination)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);

        try
        {
            var exchange = config?.Exchanges?[destination];
            if (exchange == null || exchange.Name == null)
            {
                throw new ArgumentException($"Could not find the exchange for '{destination}' in the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
            }
            return exchange;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Could not find the exchange for '{destination}' in the section {nameof(RabbitPublisherConfig)}. Please check your configuration.", ex);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                connectionPool.Dispose();
            }
            disposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public abstract class AbstractRabbitPublisher<T> : AbstractPublisherHandler<T>
{
    protected readonly string exchangeName;
    protected readonly string routingKey;
    protected readonly TimeSpan confirmTimeout;
    protected readonly IRabbitConnectionPool pool;
    // protected RabbitChannel? channel;
    readonly private ILogger logger;
    protected bool IsInitialized { get; private set; }

    protected AbstractRabbitPublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string key)
    : base()
    {
        logger = ILoggerFactory.CreateLogger<AbstractRabbitPublisher<T>>();
        confirmTimeout = timeout;
        exchangeName = exchange;
        routingKey = key;
        IsInitialized = false;
        pool = connectionPool;
    }

    protected override void Dispose(bool disposing)
    {
        if(!disposed)
        {
            if(disposing)
            {
                // pool.Dispose();
            }
            disposed = true;
        }
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

public class RabbitConnectionPool : IRabbitConnectionPool
{
    readonly Pool<RabbitConnection, IConnection> pool;
    readonly ILogger logger;

    public IPoolStats? Stats => pool?.Stats;

    public RabbitConnectionPool(
        RabbitPublisherConfig config, 
        ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<RabbitConnectionPool>();

        var factory = new ConnectionFactory
        {
            ClientProvidedName = config.ClientName ?? "FastCSharp.RabbitPublisher"
        };

        if(config.HostName != null)     factory.HostName = config.HostName;
        if(config.Port != null)         factory.Port = (int) config.Port;
        if(config.VirtualHost != null)  factory.VirtualHost = config.VirtualHost;
        if(config.Password != null)     factory.Password = config.Password;
        if(config.UserName != null)     factory.UserName = config.UserName;
        if(config.Heartbeat != null)    factory.RequestedHeartbeat = (TimeSpan) config.Heartbeat;

        var poolConfig = PoolConfigOrDefaults(config.Pool);

        // TODO: pass initizalization and timeout from configuration
        pool = new Pool<RabbitConnection, IConnection>(
            () => CreateConnection(factory, config.Hosts, loggerFactory),
            poolConfig.MinSize, 
            poolConfig.MaxSize, 
            poolConfig.Initialize, 
            poolConfig.GatherStats, 
            poolConfig.DefaultWaitTimeout.TotalMilliseconds
        );
    }

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

    static private int instanceCount = 0;

    private RabbitConnection CreateConnection(ConnectionFactory factory, IList<AmqpTcpEndpoint>? endpoints, ILoggerFactory loggerFactory)
    {
        logger.LogInformation($"Created RabbitConnection #{instanceCount++}");
        IConnection? connection;
        try
        {
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