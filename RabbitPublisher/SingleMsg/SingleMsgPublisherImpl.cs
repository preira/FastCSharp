using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using FastCSharp.Publisher;
using System.Text.Json;
using FastCSharp.RabbitPublisher.Common;

namespace FastCSharp.RabbitPublisher.Impl;

public abstract class AbstractRabbitSinglePublisher<T> : AbstractRabbitPublisher<T>, IPublisher<T>
{
    readonly private ILogger logger;

    protected AbstractRabbitSinglePublisher(
        IFCSConnection factory,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string key = "")
    : base(factory, ILoggerFactory, exchange, timeout, key)
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
        if (disposed)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        } 
        foreach (var handler in handlers)
        {
            message = await handler(message);
        } 
        byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes<T?>(message);
        return await Publish(jsonUtf8Bytes);   
    }

    private async Task<bool> Publish(byte[] message)
    {
        if (IsHealthyOrTryRecovery())
        {
            Task<bool> task = new ( () => AsyncPublish(message) );
            task.Start();

            return await task;
        }
        else
        {
            return false;
        }
    }
    protected bool AsyncPublish(byte[] body)
    {
        try
        {
            ulong? sequenceNumber = channel?.NextPublishSeqNo;
            channel.BasicPublish(exchange: exchangeName,
                routingKey: routingKey,
                basicProperties: null,
                body: body);

            channel?.WaitForConfirmsOrDie(confirmTimeout);
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
public class DirectRabbitPublisher<T> : AbstractRabbitSinglePublisher<T>, IDirectPublisher
{
    readonly private ILogger logger;
    public DirectRabbitPublisher(
        IFCSConnection factory,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string routingKey)
        : base(factory, ILoggerFactory, exchange, timeout, key: routingKey)
    {
        logger = ILoggerFactory.CreateLogger<DirectRabbitPublisher<T>>();
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

public class FanoutRabbitPublisher<T> : AbstractRabbitSinglePublisher<T>, IFanoutPublisher
{
    public FanoutRabbitPublisher(
        IFCSConnection factory, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout)
        : base(factory, ILoggerFactory, exchange, timeout)
    {
    }

    protected override void ResourceDeclarePassive(IModel channel) => channel.ExchangeDeclarePassive(exchangeName);

}

public class TopicRabbitPublisher<T> : AbstractRabbitSinglePublisher<T>, ITopicPublisher
{
    public TopicRabbitPublisher(
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

