using FastCSharp.RabbitCommon;
using Microsoft.Extensions.Logging;
using FastCSharp.Publisher;
using System.Text.Json;
using FastCSharp.RabbitPublisher.Common;

namespace FastCSharp.RabbitPublisher.Impl;

public abstract class AbstractRabbitSinglePublisher<T> : AbstractRabbitPublisher<T>, IPublisher<T>
{
    readonly private ILogger logger;

    protected AbstractRabbitSinglePublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string key = "")
    : base(connectionPool, ILoggerFactory, exchange, timeout, key)
    {
        logger = ILoggerFactory.CreateLogger<AbstractRabbitSinglePublisher<T>>();
    }

    /// <summary>
    /// Will publish the object passed as argument in JSon format, according to
    /// the underlaying implementation.
    /// </summary>
    /// <param name="message">The object to publish.</param>
    /// <returns>A Boolean future.</returns>
    public virtual async Task<bool> Publish(T? message)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);

        foreach (var handler in handlers)
        {
            message = await handler(message);
        } 
        byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes<T?>(message);
        return await Publish(jsonUtf8Bytes);   
    }

    private async Task<bool> Publish(byte[] message)
    {
        Task<bool> task = new ( () => AsyncPublish(message) );
        task.Start();

        return await task;
    }

    protected bool AsyncPublish(byte[] body)
    {
        try
        {
            using var connection = pool.Connection(this);
            using var channel = connection.Channel(this, exchangeName, routingKey);
            channel?.BasicPublish(this, 
                basicProperties: null,
                body: body);

            channel?.WaitForConfirmsOrDie(this, confirmTimeout);
            logger.LogDebug("{\"Exchange\"=\"{exchange}\", \"RoutingKey\"=\"{key}\"}", 
                            exchangeName, routingKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("[ERROR SENDING] {Message}", ex.Message);
        }
        return false;
    }
}
public class DirectRabbitPublisher<T> : AbstractRabbitSinglePublisher<T>, IDirectPublisher
{
    readonly private ILogger logger;
    public DirectRabbitPublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string routingKey)
        : base(connectionPool, ILoggerFactory, exchange, timeout, key: routingKey)
    {
        logger = ILoggerFactory.CreateLogger<DirectRabbitPublisher<T>>();
    }
}

public class FanoutRabbitPublisher<T> : AbstractRabbitSinglePublisher<T>, IFanoutPublisher
{
    public FanoutRabbitPublisher(
        IRabbitConnectionPool connectionPool, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout)
        : base(connectionPool, ILoggerFactory, exchange, timeout)
    {
    }
}

public class TopicRabbitPublisher<T> : AbstractRabbitSinglePublisher<T>, ITopicPublisher
{
    public TopicRabbitPublisher(
        IRabbitConnectionPool connectionPool, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout, 
        string routingKey)
        : base(connectionPool, ILoggerFactory, exchange, timeout, routingKey)
    {
    }
}

