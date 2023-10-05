using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FastCSharp.SDK.Publisher;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

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
    public bool IsOpen => connection?.IsOpen ?? false;
    private readonly Pool<IModel> channels;
    readonly private ILogger logger;
    private bool disposed;
    public RabbitConnection(IConnectionFactory connectionFactory, ILoggerFactory ILoggerFactory, IList<AmqpTcpEndpoint>? hosts)
    {
        endpoints = hosts;
        logger = ILoggerFactory.CreateLogger<RabbitConnection>();
        factory = connectionFactory;

        // TODO: min and max should come from the configuration
        channels = new (Create, 3, 15);
    }

    public IModel CreateChannel()
    {
        return channels.Get();
    }
    public IModel Create()
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);
        if (connection == null || !connection.IsOpen)
        {
            ResetConnection();
        }
        var channel = connection?.CreateModel();
        if(channel == null)
        {
            throw new Exception("FastCSharp could not create a new channel.");
        }
        // channels.Append(channel);
        return channel;
    }
    public bool DisposeChannel(IModel channel)
    {
        return channels.Return(channel);
    }
    public void Close() 
    {
        channels.Dispose();
        if(connection?.IsOpen ?? false)
        {
            connection.Close();
        }
    } 
    public bool ResetConnection(bool dispose = true)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);
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

            return false;        
        }
        return true;
    }    
    public void Dispose() 
    {
        Close();
        connection?.Dispose();
        disposed = true;
    } 
}

public abstract class AbstractRabbitExchangeFactory : IDisposable
{
    protected RabbitPublisherConfig config = new();
    
    protected readonly IFCSConnection connectionFactory;
    protected ILoggerFactory loggerFactory;
    protected bool disposed = false;

    protected AbstractRabbitExchangeFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        config = options.Value;
        connectionFactory = CreateRabbitConnection(config, loggerFactory);
    }

    protected AbstractRabbitExchangeFactory(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        var section = configuration.GetSection(nameof(RabbitPublisherConfig));
        section.Bind(config);
        
        connectionFactory = CreateRabbitConnection(config, loggerFactory);
    }

    private static RabbitConnection CreateRabbitConnection(RabbitPublisherConfig config, ILoggerFactory loggerFactory)
    {
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
        
        return new RabbitConnection(factory, loggerFactory, config.Hosts);
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
                connectionFactory.Dispose();
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
        ResetChannel(dispose: false);
    }

    protected override bool ResetChannel(bool dispose = true)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);
        try
        {
            // TODO: pass responsibility to the connection factory
            if (channel != null)
            {
                connection.DisposeChannel(channel);
            }

            channel = connection.CreateChannel();
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
        // TODO: return to the channels' pool
        if(!disposed)
        {
            if(disposing)
            {
                if (channel != null)
                {
                    connection.DisposeChannel(channel);
                }
                // channel?.Dispose();
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


public delegate T Create<T>();
public class Pool<T>  : IDisposable
where T : class, IDisposable
{
    private Create<T> Factory { get; set; }
    readonly ConcurrentQueue<T> available;
    readonly ConcurrentDictionary<int, WeakReference<T>> inUse;
    public int MinSize { get; private set;}
    public int MaxSize { get; private set;}
    private int count;
    public int Count { get => count; }
    private int idx = 0;
    private bool disposed;

    private int Index {
        get => Interlocked.Exchange(ref idx, idx = (idx + 1) % MaxSize);
    }
    
    public readonly object _lock = new ();

    // TODO: implement a connection pool
    // Add pool statistics
    public Pool(Create<T> factory, int minSize, int maxSize)
    {
        Factory = factory;

        MinSize = minSize;
        MaxSize = maxSize;

        available = new ();
        inUse = new ();
    }

    public T Get(int timeout = 1000)
    {
        if (disposed) throw new ObjectDisposedException(GetType().FullName);
        try
        {
            var timeLimit = DateTime.Now.AddMilliseconds(timeout);
            Monitor.Enter(_lock);

Console.WriteLine($"[WAIT ??] available.IsEmpty: {available.IsEmpty} Count >= MaxSize: {Count} >= {MaxSize}");
            while (available.IsEmpty && Count >= MaxSize)
            {
this.PurgeInUse();
                var remaining = timeLimit - DateTime.Now;
                if (remaining <= TimeSpan.Zero) 
                {
                    throw new TimeoutException("Could not get a connection from the pool within the timeout.");
                }
                    
Console.WriteLine($"[WAITING] for: {remaining}");
                bool timedout = Monitor.Wait(_lock, remaining);
                
                if (!available.IsEmpty || Count < MaxSize) break;

                if (timedout)
                {
                    throw new TimeoutException("Could not get a connection from the pool within the timeout.");
                }
            }

            if (available.TryDequeue(out var element))
            {
                inUse[Index] = new WeakReference<T>(element);
Console.WriteLine($"[POOL] Got from Pool: {element.GetHashCode()}");
                Monitor.Pulse(_lock);
                return element;
            }
            if (Count < MaxSize)
            {
                Interlocked.Increment(ref count);
                var newElement = Factory();
                inUse[Index] = new WeakReference<T>(newElement);
Console.WriteLine($"[POOL] Created new: {newElement.GetHashCode()} - Count: {Count}; Index: {Index}");
                Monitor.Pulse(_lock);
                return newElement;
            }
            throw new Exception("If you are reading this, something is wrong with the pool implementation.");
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public bool Return(T element)
    {
        if (disposed) return false;
        try
        {
            Monitor.Enter(_lock);
            
            var entry = inUse.Where(e => e.Value.TryGetTarget(out var target) && target == element).FirstOrDefault();
            if (entry.Key != 0)
            {
                inUse.TryRemove(entry.Key, out _);
                if(available.Count <= MinSize)
                {
                    available.Enqueue(element);
Console.WriteLine($"[POOL] Returned to Pool: {element.GetHashCode()}");
                }
                else
                {
                    element.Dispose();
                    Interlocked.Decrement(ref count);
Console.WriteLine($"[POOL] Pool full disposing channel: {element.GetHashCode()}");
                }
                Monitor.Pulse(_lock);
                return true;
            }
            else
            {
Console.WriteLine($"[POOL] NOT in the Pool: {element.GetHashCode()}");
                // If it is not in the inUse list, it is not a valid connection.
                element.Dispose();
                return false;
            }
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public void PurgeInUse()
    {
        if (disposed) return;
        try
        {
            Monitor.Enter(_lock);
Console.WriteLine($"[PURGING] pool inUse.Count: {inUse.Count} available.Count: {available.Count} Count: {Count}");
            inUse
                .Where(e => !e.Value.TryGetTarget(out var target))
                .ToList()
                .ForEach(e => inUse.TryRemove(e.Key, out _));
            Interlocked.Exchange(ref count, inUse.Count + available.Count);
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        try
        {
            if (!disposed)
            {
                if (disposing)
                {
 Console.WriteLine($"[POOL] Disposing queue inUse.Count: {inUse.Count} available.Count: {available.Count} Count: {Count}");
                   // dispose managed state (managed objects)
                disposed = true;
                    Monitor.TryEnter(_lock, 5000);
                    foreach (var element in available)
                    {
                        element.Dispose();
                    }
                    for(int i = 0; i < Count; i++)
                        inUse.TryRemove(i, out _);
                }

                Monitor.PulseAll(_lock);
            }
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}