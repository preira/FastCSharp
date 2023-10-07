using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Common;

namespace FastCSharp.RabbitPublisher.Impl;

public abstract class AbstractRabbitBatchPublisher<T> : AbstractRabbitPublisher<T>, IBatchPublisher<T>
{
    readonly private ILogger logger;
    protected AbstractRabbitBatchPublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string key = "")
    : base(connectionPool, ILoggerFactory, exchange, timeout, key)
    {
        logger = ILoggerFactory.CreateLogger<AbstractRabbitBatchPublisher<T>>();
    }

    /// <summary>
    /// Will publish the whole list of messages passed as argument and await for confirmation at the
    /// end of the batch.
    /// </summary>
    /// <param name="messages">The list of messages to publish.</param>
    /// <returns>A Boolean future that indicates if the whole batch has been published
    /// or if a problem occured.</returns>
    public async Task<bool> Publish(IEnumerable<T> messages)
    {
        try
        {
            using var connection = pool.Connection(this);
            using var channel = connection.Channel(this, exchangeName, routingKey);

            foreach (var message in messages)
            {
                var msg = message;
                foreach (var handler in handlers)
                {
                    msg = await handler(msg);
                }
                byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes<T?>(msg);
                Task<bool> task = new ( () => AsyncPublish(channel, jsonUtf8Bytes) );
                task.Start();

                bool isOk = await task;
                if (!isOk)
                {
                    // Something went wrong. Stop the batch.
                    return false;
                }
            }
            Task<bool> confirm = new ( () => Confirm(channel) );
            confirm.Start();
            return await confirm;
        }
        catch (Exception ex)
        {
            logger.LogError("[ERROR SENDING] {Message}", ex.Message);
            return false;
        }
    }

    // TODO: async confirm can be implemented using yeld return and a concurrent dictionary 
    // * using also a TaskCompletionSource ??
    protected bool Confirm(IRabbitChannel channel)
    {
        try
        {
            channel?.WaitForConfirmsOrDie(this, confirmTimeout);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("[ERROR SENDING] {Message}", ex.Message);
        }
        return false;
    }

    /// <summary>
    /// Async publish a message. In this version, the message is not confirmed.
    /// </summary>
    /// <param name="body">message content in bytes</param>
    /// <returns>True if the message was published to the channel. False if an exception is raised.</returns>
    protected bool AsyncPublish(IRabbitChannel channel, byte[] body)
    {
        try
        {
            ulong? sequenceNumber = channel?.NextPublishSeqNo(this);
            channel?.BasicPublish(this, 
                basicProperties: null,
                body: body);

            logger.LogDebug("{\"Exchange\"=\"{exchange}\", \"RoutingKey\"=\"{key}\", \"SequenceNumber\"=\"{seqNr}\"}",
                            exchangeName, routingKey, sequenceNumber);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("[ERROR SENDING] {Message}", ex.Message);
        }
        return false;
    }

}

public class DirectRabbitBatchPublisher<T> : AbstractRabbitBatchPublisher<T>, IDirectPublisher
{
    readonly private ILogger logger;
    public DirectRabbitBatchPublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string routingKey)
        : base(connectionPool, ILoggerFactory, exchange, timeout, key: routingKey)
    {
        logger = ILoggerFactory.CreateLogger<DirectRabbitBatchPublisher<T>>();
    }
}

public class FanoutRabbitBatchPublisher<T> : AbstractRabbitBatchPublisher<T>, IFanoutPublisher
{
    public FanoutRabbitBatchPublisher(
        IRabbitConnectionPool connectionPool, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout)
        : base(connectionPool, ILoggerFactory, exchange, timeout)
    {
    }
}


public class TopicRabbitBatchPublisher<T> : AbstractRabbitBatchPublisher<T>, ITopicPublisher
{
    public TopicRabbitBatchPublisher(
        IRabbitConnectionPool connectionPool, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout, 
        string routingKey)
        : base(connectionPool, ILoggerFactory, exchange, timeout, routingKey)
    {
    }
}





