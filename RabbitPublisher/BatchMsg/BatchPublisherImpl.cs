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
        IFCSConnection factory,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string key = "")
    : base(factory, ILoggerFactory, exchange, timeout, key)
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
    public async Task<bool> BatchPublish(IEnumerable<T> messages)
    {
        if (IsHealthyOrTryRecovery())
        {
            foreach (var message in messages)
            {
                var msg = message;
                foreach (var handler in handlers)
                {
                    msg = await handler(msg);
                }
                byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes<T?>(msg);
                Task<bool> task = new ( () => AsyncPublish(jsonUtf8Bytes) );
                task.Start();

                bool isOk = await task;
                if (!isOk)
                {
                    // Something went wrong. Stop the batch.
                    return false;
                }
            }
            Task<bool> confirm = new ( () => Confirm() );
            confirm.Start();
            return await confirm;
        }
        else
        {
            return false;
        }
    }

    // TODO: async confirm can be implemented using yeld return and a concurrent dictionary 
    // * using also a TaskCompletionSource ??
    protected bool Confirm()
    {
        try
        {
            channel?.WaitForConfirmsOrDie(confirmTimeout);
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
    /// <param name="body"></param>
    /// <returns>True if the message was published to the channel. False if an exception is raised.</returns>
    protected bool AsyncPublish(byte[] body)
    {
        try
        {
            ulong? sequenceNumber = channel?.NextPublishSeqNo;
            channel.BasicPublish(exchange: exchangeName,
                routingKey: routingKey,
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
        IFCSConnection factory,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string routingKey)
        : base(factory, ILoggerFactory, exchange, timeout, key: routingKey)
    {
        logger = ILoggerFactory.CreateLogger<DirectRabbitBatchPublisher<T>>();
    }

    protected override void ResourceDeclarePassive(IModel channel)
    {
        channel.ExchangeDeclarePassive(exchangeName);
        channel.QueueDeclarePassive(routingKey);
    }

    override protected bool IsHealthy()
    {
        if (channel != null)
        {
            try
            {
                // WaitForConfirmsOrDie already breaks when the exchange is unkown.
                // So, no need to check the exchange.
                channel.QueueDeclarePassive(routingKey);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError("[WARNING] ${message}", ex.Message);
            }
        }
        return false;
    }
}

public class FanoutRabbitBatchPublisher<T> : AbstractRabbitBatchPublisher<T>, IFanoutPublisher
{
    public FanoutRabbitBatchPublisher(
        IFCSConnection factory, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout)
        : base(factory, ILoggerFactory, exchange, timeout)
    {
    }

    protected override void ResourceDeclarePassive(IModel channel) => channel.ExchangeDeclarePassive(exchangeName);

}


public class TopicRabbitBatchPublisher<T> : AbstractRabbitBatchPublisher<T>, ITopicPublisher
{
    public TopicRabbitBatchPublisher(
        IFCSConnection factory, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout, 
        string routingKey)
        : base(factory, ILoggerFactory, exchange, timeout, routingKey)
    {
    }

    protected override void ResourceDeclarePassive(IModel channel) => channel.ExchangeDeclarePassive(exchangeName);

}





