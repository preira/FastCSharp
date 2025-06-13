using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using FastCSharp.Pool;
using System.Collections.Concurrent;

namespace FastCSharp.RabbitPublisher.Common;

public class RabbitConnection : Individual<IConnection>, IRabbitConnection
{
    public bool IsOpen => connection.IsOpen;
    private readonly IConnection connection;
    private PoolConfig PoolConfig { get; }
    private readonly ConcurrentDictionary<Tuple<string, string?, string?>, AsyncPool<RabbitChannel, IChannel>> channelsPools;
    readonly private ILogger logger;
    private ILoggerFactory LoggerFactory { get; set; }

    public RabbitConnection(IConnection connection, ILoggerFactory loggerFactory, PoolConfig? poolConfig = null)
    : base(connection)
    {
        this.connection = connection;
        LoggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<RabbitConnection>();
        channelsPools = new();

        PoolConfig = poolConfig ??
            new PoolConfig
            {
                MinSize = 1,
                MaxSize = 10,
                DefaultWaitTimeout = TimeSpan.FromMilliseconds(1000),
                GatherStats = false,
                Initialize = false
            };
    }

    public async Task<IRabbitChannel> GetChannelAsync(object owner, string exchangeName, string? queue, string? routingKey)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

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
                PoolConfig
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

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
