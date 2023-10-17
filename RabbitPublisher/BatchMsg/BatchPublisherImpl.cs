using Microsoft.Extensions.Logging;
using System.Text.Json;
using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Common;
using RabbitMQ.Client.Exceptions;

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

            IList<Task<bool>> tasks = new List<Task<bool>>();
            try
            {
                foreach (var message in messages)
                {
                    var msg = message;
                    foreach (var handler in handlers)
                    {
                        msg = await handler(msg);
                    }
                    byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes<T?>(msg);

                    ulong? sequenceNumber = channel.NextPublishSeqNo(this);
                    channel.BasicPublish(this, 
                        basicProperties: null,
                        body: jsonUtf8Bytes);

                    logger.LogDebug("{\"Exchange\"=\"{exchange}\", \"RoutingKey\"=\"{key}\", \"SequenceNumber\"=\"{seqNr}\"}",
                                    exchangeName, routingKey, sequenceNumber);

                }
                channel.WaitForConfirmsOrDie(this, confirmTimeout);
                return true;
            }
            catch (AlreadyClosedException ace)
            {
                logger.LogError("[ERROR WAITING FOR CONFIRMATION: CHANNEL CLOSED] {Exception}: {Message}", ace.GetType().FullName, ace.Message);
                logger.LogDebug(ace.StackTrace);
                channel.IsStalled = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError("[ERROR BATCH SENDING] {Exception}: {Message}", ex.GetType().FullName, ex.Message);
            logger.LogDebug(ex.StackTrace);

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





