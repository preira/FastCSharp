using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FastCSharp.SDK.Publisher;
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

public class RabbitConnection : IFCSConnection
{
    private readonly IConnectionFactory factory;
    private readonly IList<AmqpTcpEndpoint>? endpoints;
    private IConnection? connection;
    private readonly ConcurrentQueue<IModel> channels;
    readonly private ILogger logger;
    public RabbitConnection(IConnectionFactory connectionFactory, ILoggerFactory ILoggerFactory, IList<AmqpTcpEndpoint>? hosts)
    {
        endpoints = hosts;
        logger = ILoggerFactory.CreateLogger<RabbitConnection>();
        factory = connectionFactory;

        channels = new ();
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
        channels.Append(channel);
        return channel;
    }
    public void Close() 
    {
        while(channels.TryDequeue(out var channel))
        {
            if(!channel.IsClosed)
            {
                channel.Close();
                channel.Dispose();
            }
        }
        if(connection?.IsOpen ?? false)
        {
            connection.Close();
        }
    } 
    public bool ResetConnection(bool dispose = true)
    {
        try
        {
            connection?.Close();
            connection?.Dispose();

            if(endpoints == null)
            {
                connection = factory.CreateConnection();
            }
            else
            {
                connection = factory.CreateConnection(endpoints);
            }
        }
        catch (Exception ex)
        {
            if (dispose)
            {
                var debugFactory =  factory as ConnectionFactory;

                var error = $"\tFactory URI: {factory.Uri}\n";
                error += $"\tFactory endpoint: {debugFactory?.HostName}:{debugFactory?.Port}\n";

                if(endpoints != null)
                {
                    var i = 0;
                    endpoints.ToList().ForEach(e =>
                        error += $"\tendpoint {++i}: {e}\n"
                    );
                }
                var pw = debugFactory?.Password;
                string? staredPw = null;

                if (pw!=null)
                {
                    staredPw = $"{pw[..1]}******{pw[^2..]}";
                }
                error += $"\nThis may be due to incorrect authentication. Please check your user ('{debugFactory?.UserName}') and password ('{staredPw}').";
                logger.LogError("[CONFIG ERROR] {message}\n{error}\n", ex.Message, error);
            }
            else
            {
                logger.LogError("[INITIALIZATION ERROR] {messsage}", ex.Message);
            }
            logger.LogDebug("{stackTrace}", ex.GetType());
            logger.LogDebug("{stackTrace}", ex.StackTrace);

            return false;        }
        return true;
    }    
    public void Dispose() 
    {
        Close();
        connection?.Dispose();
    } 
}

public abstract class AbstractRabbitExchangeFactory : IDisposable
{
    protected RabbitPublisherConfig config = new();
    
    protected readonly IFCSConnection connectionFactory;
    protected ILoggerFactory ILoggerFactory;
    private bool disposedValue;

    protected AbstractRabbitExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    {
        this.ILoggerFactory = ILoggerFactory;
        var section = configuration.GetSection(nameof(RabbitPublisherConfig));
        section.Bind(config);

        var factory = new ConnectionFactory
        {
            ClientProvidedName = config.ClientName ?? "FastCSharp.RabbitPublisher"
        };

        if (config.HostName != null) factory.HostName = config.HostName;
        if(config.Port != null) factory.Port = (int) config.Port;
        if(config.VirtualHost != null) factory.VirtualHost = config.VirtualHost;
        if(config.Password != null) factory.Password = config.Password;
        if(config.UserName != null) factory.UserName = config.UserName;
        if(config.Heartbeat != null) factory.RequestedHeartbeat = (TimeSpan) config.Heartbeat;
        
        connectionFactory = new RabbitConnection(factory, ILoggerFactory, config.Hosts);
    }

    protected ExchangeConfig _NewPublisher(string destination)
    {
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
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                connectionFactory.Dispose();
            }
            disposedValue = true;
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
    protected readonly IFCSConnection connection;
    protected IModel? channel;
    readonly private ILogger logger;
    protected bool IsInitialized { get; private set; }

    protected AbstractRabbitPublisher(
        IFCSConnection factory,
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
        connection = factory;
        IsInitialized = false;
        Init();
    }

    protected override bool IsHealthy() => IsInitialized;

    private void Init()
    {
        ResetConnection(dispose: false);
    }

    protected override bool ResetConnection(bool dispose = true)
    {
        try
        {
            channel?.Dispose();

            channel = connection.CreateModel();
            channel.ConfirmSelect();

            ResourceDeclarePassive(channel);
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
            logger.LogDebug("{stackTrace}", ex.StackTrace);
            IsInitialized = false;
            return false;
        }
        IsInitialized = true;
        return true;
    }

    protected abstract void ResourceDeclarePassive(IModel channel);

    protected override void Dispose(bool disposing)
    {
        if(!disposed)
        {
            if(disposing)
            {
                channel?.Dispose();
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

