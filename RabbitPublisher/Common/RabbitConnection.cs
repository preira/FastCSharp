using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using FastCSharp.Pool;
using System.Collections.Concurrent;

namespace FastCSharp.RabbitPublisher.Common;

public class RabbitConnection : Individual<IConnection>, IRabbitConnection
{
    private readonly IConnection connection;
    public bool IsOpen => connection.IsOpen;
    private readonly ConcurrentDictionary<Tuple<string, string?, string?>, AsyncPool<RabbitChannel, IChannel>> channelsPools;
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

    public async Task<IRabbitChannel> GetChannelAsync(object owner, string exchangeName, string? queue, string? routingKey)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);

        AsyncPool<RabbitChannel, IChannel>? pool;
        bool poolExists = channelsPools.TryGetValue(Tuple.Create(exchangeName, queue, routingKey), out pool);

        if(!poolExists)
        {
            logger.LogDebug($"Creating new channel pool for exchange '{exchangeName}' and queue '{queue}'.");
            // TODO: pass min, max, initialize defaultTimeout and gather stats from configuration
            // if it doesn't exist create a new pool after verifying the exchange and routing key
            pool = new AsyncPool<RabbitChannel, IChannel>(
                async () => await CreateAsync(exchangeName, queue, routingKey),
                minObjects, maxObjects, initialize, gatherStats, defaultTimeout
            );
            _ = await CreateAsync(exchangeName, queue, routingKey);
            channelsPools.TryAdd(Tuple.Create(exchangeName, queue, routingKey), pool);
        }

        if(pool == null)
        {
            throw new Exception("FastCSharp could not create a new channel.");
        }

        return await pool.BorrowAsync(owner);
    }
    
    private async Task<RabbitChannel> CreateAsync(string exchangeName, string? queue, string? routingKey, bool confirms = true)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);
        var channel = await connection!.CreateChannelAsync(
            new CreateChannelOptions(confirms, confirms)
        );
        if(channel == null)
        {
            throw new Exception("FastCSharp could not create a new channel.");
        }
        return new RabbitChannel(channel, exchangeName, queue, routingKey);
    }

    public async Task CloseAsync() 
    {
        channelsPools.ToList().ForEach(e => e.Value.Dispose());
        channelsPools.Clear();
        if(connection?.IsOpen ?? false)
        {
            await connection.CloseAsync();
        }
    } 

    public async Task DisposeValue() 
    {
        await CloseAsync();
        connection?.Dispose();
        disposed = true;
    }
}
