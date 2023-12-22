using FastCSharp.Subscriber;
using FastCSharp.Exceptions;
using FastCSharp.SDK.Subscriber;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FastCSharp.Observability;

namespace FastCSharp.RabbitSubscriber.Impl;

public class RabbitSubscriber<T> : AbstractSubscriber<T>
{
    readonly private IConnectionFactory connectionFactory;
    private QueueConfig QConfig { get;}
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

    public override IConfigurationSection? Options { get; }

    public RabbitSubscriber(
            IConnectionFactory connectionFactory,
            QueueConfig queue,
            ILoggerFactory loggerFactory,
            IList<AmqpTcpEndpoint>? hosts
        ) : base()
    {
        endpoints = hosts;
        this.connectionFactory = connectionFactory;
        QConfig = queue;
        Options = QConfig.Options;

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
        channel.QueueDeclarePassive(queue: QConfig.Name);

        // BasicQos configuration setting from config read.
        channel.BasicQos(QConfig.PrefetchSize ?? 0, QConfig.PrefetchCount ?? 0, global: false);

        var consumer = new EventingBasicConsumer(channel);

        if (_callback == null)
        {
            throw new IncorrectInitializationException("Callback not registered. Check your implementation.");
        }
        consumer.Received += GetListener(_callback);

        channel.BasicConsume(queue: QConfig.Name,
                            autoAck: false,
                            consumer: consumer,
                            consumerTag: ConsumerTag);

        logger.LogInformation("Waiting for messages from queue {messageOrigin}.", QConfig.Name);
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
        try
        {
            // this throws an null pointer exception if the consumer has never registered before.
            channel.BasicCancel(ConsumerTag);
        }
        catch (NullReferenceException)
        {
            logger.LogWarning("Consumer was never registered. Ignoring.");
        }
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

    public override async Task<IHealthReport> ReportHealthStatus()
    {
        return await Task.Run(() => 
        {
            var status = connection.IsOpen ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            var report = new HealthReport(GetType().Name, status)
            {
                Description = $"RabbitMQ Connection Status: {status}"
            };
            return report;
        });
    }
}

