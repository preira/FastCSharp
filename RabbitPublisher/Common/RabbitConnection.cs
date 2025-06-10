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
    private ILoggerFactory LoggerFactory { get; set; }

    // TODO: pass min, max, defaultTimeout and gather stats from configuration
    public RabbitConnection(IConnection connection, ILoggerFactory loggerFactory)
    : base(connection)
    {
        this.connection = connection;
        LoggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<RabbitConnection>();
        channelsPools = new();
        minObjects = 1;
        maxObjects = 10;
        defaultTimeout = 1000;
    }

    public async Task<IRabbitChannel> GetChannelAsync(object owner, string exchangeName, string? queue, string? routingKey)
    {
        if(IsDisposed) throw new ObjectDisposedException(GetType().FullName);

        AsyncPool<RabbitChannel, IChannel>? pool;
        bool poolExists = channelsPools.TryGetValue(Tuple.Create(exchangeName, queue, routingKey), out pool);

        if(!poolExists)
        {
            if(logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Channel pool for exchange '{ExchangeName}' and queue '{Queue}' does not exist. Creating new pool.", exchangeName, queue);
            }

            pool = new AsyncPool<RabbitChannel, IChannel>(
                async () => await CreateAsync(exchangeName, queue, routingKey),
                LoggerFactory,
                minObjects, maxObjects, initialize, gatherStats, defaultTimeout
            );
            channelsPools.TryAdd(Tuple.Create(exchangeName, queue, routingKey), pool);
        }

        if(pool == null)
        {
            throw new InvalidOperationException("FastCSharp could not create a new channel.");
        }

        return await pool.BorrowAsync(owner);
    }
    
    private async Task<RabbitChannel> CreateAsync(string exchangeName, string? queue, string? routingKey, bool confirms = true)
    {
        if(IsDisposed) throw new ObjectDisposedException(GetType().FullName);
        var channel = await connection!.CreateChannelAsync(
            new CreateChannelOptions(confirms, confirms)
        );
        if(channel == null)
        {
            throw new InvalidOperationException("FastCSharp could not create a new channel.");
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
        IsDisposed = true;
    }
}
