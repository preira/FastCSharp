using FastCSharp.Subscriber;
using FastCSharp.Exceptions;
using FastCSharp.SDK.Subscriber;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FastCSharp.RabbitSubscriber.Impl;

/// <summary>
/// RabbitMQ Queue Configuration.
/// </summary>
public class RabbitQueueConfig
{
    /// <summary>
    /// The name of the queue.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// The maximum number of messages that the server will deliver, 0 if unlimited.
    /// </summary>
    public ushort? PrefetchCount { get; set; }
    /// <summary>
    /// Specifies a prefetch window in octets.
    /// </summary>
    /// <value></value>
    /// <remarks>
    /// The server will send a message in advance if it is equal to or smaller in size than 
    /// the available prefetch size (and also falls into other prefetch limits).
    /// May be set to zero, meaning "no specific limit", although other prefetch limits may still apply.
    /// The prefetch-size is ignored if the no-ack option is set.
    /// </remarks>
    /// <seealso href="https://www.rabbitmq.com/consumer-prefetch.html"/>
    public uint? PrefetchSize { get; set; }
}

public class RabbitSubscriber<T> : AbstractSubscriber<T>
{
    readonly private IConnectionFactory connectionFactory;
    readonly private RabbitQueueConfig q;
    private readonly IList<AmqpTcpEndpoint>? endpoints;
    private IConnection connection;
    private IModel channel;

    readonly private ILogger<RabbitSubscriber<T>> logger;
    private OnMessageCallback<T>? _callback;
    private bool disposedValue;
    readonly private string _consumerTag;
    public string ConsumerTag
    {
        get => _consumerTag;
    }
    public RabbitSubscriber(
            IConnectionFactory connectionFactory,
            RabbitQueueConfig queue,
            ILoggerFactory loggerFactory,
            IList<AmqpTcpEndpoint>? hosts
        ) : base()
    {
        endpoints = hosts;
        this.connectionFactory = connectionFactory;
        q = queue;

        logger = loggerFactory.CreateLogger<RabbitSubscriber<T>>();

        if(endpoints == null)
        {
            connection = connectionFactory.CreateConnection();
        }
        else
        {
            connection = connectionFactory.CreateConnection(endpoints);
        }
        channel = connection.CreateModel();

        _consumerTag = Guid.NewGuid().ToString();
    }

    public void ResetConnection()
    {
        if(!connection.IsOpen)
        {
            connection.Close();
            channel.Dispose();
            connection.Dispose();
            if(endpoints == null)
            {
                connection = connectionFactory.CreateConnection();
            }
            else
            {
                connection = connectionFactory.CreateConnection(endpoints);
            }
            channel = connection.CreateModel();
        }
        else if(channel.IsClosed)
        {
            channel.Dispose();
            channel = connection.CreateModel();
        }
    }

    /// <summary>
    /// Use reset to re-register the consumer.
    /// </summary>
    public override void Reset()
    {
        ResetConnection();
        RegisterConsumer();
    }

    private void RegisterConsumer()
    {

        // don't declare, just check if exists
        channel.QueueDeclarePassive(queue: q.Name);

        // BasicQos configuration setting from config read.
        channel.BasicQos(q.PrefetchSize ?? 0, q.PrefetchCount ?? 0, global: false);

        var consumer = new EventingBasicConsumer(channel);

        if (_callback == null)
        {
            throw new IncorrectInitializationException("Callback not registered. Check your implementation.");
        }
        consumer.Received += GetListener(_callback);

        channel.BasicConsume(queue: q.Name,
                            autoAck: false,
                            consumer: consumer,
                            consumerTag: ConsumerTag);

        logger.LogInformation("Waiting for messages from queue {messageOrigin}.", q.Name);
    }

    protected override void _Register(OnMessageCallback<T> callback)
    {
        _callback = callback;
        RegisterConsumer();
    }

    /// <summary>
    /// Use unsubscribe to stop receiving messages.
    /// </summary>
    public override void UnSubscribe()
    {
        channel.BasicCancel(ConsumerTag);
    }

    /// <summary>
    /// Use close channel to stop receiving messages and close the channel.
    /// If you are looking to control message consumption, consider using UnSubscribe.
    /// </summary>
    public void CloseChannel()
    {
        // For Reply Codes, refer to https://www.rfc-editor.org/rfc/rfc2821#page-42
        channel.Close(421, "Consumer temporarily unavailable.");
    }

    public EventHandler<BasicDeliverEventArgs> GetListener(OnMessageCallback<T> callback)
    {
        return async (model, ea) =>
        {
            logger.LogTrace(" [Receiving]");
            try
            {
                var body = ea.Body.ToArray();
                var message = JsonSerializer.Deserialize<T>(body);

                var success = await callback(message);
                if (success)
                {
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                else
                {
                    channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }

            }
            catch (JsonException e)
            {
                // Deserialization exception is a final exception and removes the message from the queue.
                logger.LogError("Discarding unparseable message with id: {messageId}", ea?.BasicProperties.MessageId);
                logger.LogError("Error: {message}", e.Message);
                channel.BasicNack(deliveryTag: ea?.DeliveryTag ?? 0, multiple: false, requeue: false);
            }
            catch (System.Exception e)
            {
                logger.LogError("Discarding unparseable message with id: {messageId}", ea?.BasicProperties?.MessageId);
                logger.LogError("Error: {message}", e.Message);
                channel.BasicNack(deliveryTag: ea?.DeliveryTag ?? 0, multiple: false, requeue: true);
                throw;
            }
            logger.LogTrace(" [waiting]");
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                channel?.Dispose();
                connection?.Dispose();
            }
            disposedValue = true;
        }
    }
}

