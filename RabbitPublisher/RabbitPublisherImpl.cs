using RabbitMQ.Client;
using FastCSharp.SDK.Publisher;
using Microsoft.Extensions.Logging;

namespace FastCSharp.RabbitPublisher.Impl;

public abstract class AbstractRabbitPublisher<T> : AbstractPublisher<T>
{
    protected readonly string exchangeName;
    protected readonly string routingKey;
    protected readonly TimeSpan confirmTimeout;
    protected readonly IConnectionFactory connectionFactory;
    protected IModel? channel;
    protected IConnection? connection;
    readonly private ILogger logger;
    private bool isInitialized = false;

    protected AbstractRabbitPublisher(
        IConnectionFactory factory,
        ILoggerFactory ILoggerFactory,
        string exchange,
        TimeSpan timeout,
        string key = "")
    : base()
    {
        logger = ILoggerFactory.CreateLogger<AbstractRabbitPublisher<T>>();
        confirmTimeout = timeout;
        exchangeName = exchange;
        routingKey = key;
        connectionFactory = factory;
        Init();
    }

    protected override bool AsyncPublish(byte[] body)
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
        catch (System.Exception ex)
        {
            logger.LogError("[ERROR SENDING] {Message}", ex.Message);
        }
        return false;
    }

    protected override bool IsHealthy() => isInitialized;

    private void Init()
    {
        ResetConnection(dispose: false);
    }

    protected override Boolean ResetConnection(bool dispose = true)
    {
        try
        {
            channel?.Dispose();
            connection?.Dispose();

            connection = connectionFactory.CreateConnection();
            channel = connection.CreateModel();
            channel.ConfirmSelect();

            ResourceDeclarePassive(channel);
        }
        catch (System.Exception ex)
        {
            if (dispose)
            {
                logger.LogError("[CONFIG ERROR] {message}", ex.Message);
            }
            else
            {
                logger.LogError("[INITIALIZATION ERROR] {messsage}", ex.Message);
            }
            logger.LogDebug(ex.StackTrace);
            isInitialized = false;
            return false;
        }
        isInitialized = true;
        return true;
    }

    protected abstract void ResourceDeclarePassive(IModel channel);

    public override void Dispose(bool disposing)
    {
        if(!disposed)
        {
            if(disposing)
            {
                channel?.Dispose();
                connection?.Dispose();
            }
            disposed = true;
        }
    }
}
public class DirectRabbitPublisher<T> : AbstractRabbitPublisher<T>
{
    readonly private ILogger logger;
    public DirectRabbitPublisher(
        IConnectionFactory factory,
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

    override protected Boolean IsHealthy()
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
            catch (System.Exception ex)
            {
                logger.LogError("[WARNING] ${message}", ex.Message);
            }
        }
        return false;
    }
}

public class FanoutRabbitPublisher<T> : AbstractRabbitPublisher<T>
{
    public FanoutRabbitPublisher(
        IConnectionFactory factory, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout)
        : base(factory, ILoggerFactory, exchange, timeout)
    {
    }

    protected override void ResourceDeclarePassive(IModel channel) => channel.ExchangeDeclarePassive(exchangeName);

}

public class TopicRabbitPublisher<T> : AbstractRabbitPublisher<T>
{
    public TopicRabbitPublisher(IConnectionFactory factory, 
        ILoggerFactory ILoggerFactory,
        string exchange, 
        TimeSpan timeout, 
        string routingKey)
        : base(factory, ILoggerFactory, exchange, timeout, routingKey)
    {
    }

    protected override void ResourceDeclarePassive(IModel channel) => channel.ExchangeDeclarePassive(exchangeName);

}
