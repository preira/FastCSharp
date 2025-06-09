using FastCSharp.Subscriber;
using FastCSharp.Exceptions;
using FastCSharp.SDK.Subscriber;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FastCSharp.Observability;
using RabbitMQ.Client.Exceptions;

namespace FastCSharp.RabbitSubscriber.Impl;

public class RabbitSubscriber<T> : AbstractSubscriber<T>
{
    public readonly object _lock = new ();
    readonly private IConnectionFactory connectionFactory;
    private QueueConfig QConfig { get; }
    private readonly IList<AmqpTcpEndpoint>? endpoints;
    private IConnection? connection;
    private IChannel? channel;

    readonly private ILogger<RabbitSubscriber<T>> logger;
    private OnMessageCallback<T>? _callback;
    private bool disposedValue;
    readonly private string _consumerTag;
    public string ConsumerTag
    {
        get => _consumerTag;
    }

    public override IConfigurationSection? Options { get; }

    /// <inheritdoc/>
    public override bool IsHealthy { 
        get 
        {
            var isConnected = connection != null && connection.IsOpen;
            var hasChannel = isConnected && channel != null && channel.IsOpen;
            if (!hasChannel)
            {
                return false;
            }
            try
            {
                var task = channel!.QueueDeclarePassiveAsync(QConfig.Name!);
                task.Wait();
                return task.Result.QueueName == QConfig.Name;
                
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Queue passive declare failed. Queue may not exist or connection is not healthy.");
                return false;
            }
        }
    }

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

        _consumerTag = Guid.NewGuid().ToString();
    }

    private int connectionInProgress = 0;

    /// <inheritdoc/>
    public async Task ResetConnectionAsync()
    {
        // Alllow only one thread to connect at a time.
        while(Interlocked.CompareExchange(ref connectionInProgress, 1, 0) == 0)
        {
            await Task.Delay(1);
        }
        try
        {
            await ReConnect();
        }
        finally
        {
            Interlocked.Exchange(ref connectionInProgress, 0);
        }
    }

    private async Task ReConnect()
    {
        bool isConnected = false;
        while (!isConnected)
        {
            try
            {
                if(connection == null || !connection.IsOpen)
                {
                    channel?.Dispose();
                    connection?.Dispose();

                    if(endpoints == null)
                    {
                        connection = await connectionFactory.CreateConnectionAsync();
                    }
                    else
                    {
                        connection = await connectionFactory.CreateConnectionAsync(endpoints);
                    }
                    connection.ConnectionShutdownAsync += async (sender, args) =>
                    {
                        logger.LogWarning("RabbitMQ connection shutdown: {0}. FastCSharp Client will NOT try to recover. You should have a watch dog to handle reconnection.", args.ReplyText);
                        await Task.Yield();
                    };
                    
                    channel = await connection.CreateChannelAsync();
                    channel.ChannelShutdownAsync += async (sender, args) =>
                    {
                        logger.LogWarning("RabbitMQ channel shutdown: {0}. FastCSharp Client will NOT try to recover. You should have a watch dog to handle reconnection.", args.ReplyText);
                        await Task.Yield();
                    };
                }
                else if(channel == null || channel.IsClosed)
                {
                    channel?.Dispose();
                    channel = await connection.CreateChannelAsync();
                }

                isConnected = connection.IsOpen && channel.IsOpen;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error connecting to Rabbit MQ. Retrying in 5 seconds.");
                // TODO: should be configurable. Could this take advantage of a Backoff strategy?
                await Task.Delay(5000);
            }
        }

    }

    /// <inheritdoc/>
    public override async Task ResetAsync()
    {
        await ResetConnectionAsync();
        await RegisterConsumer();
    }

    private async Task RegisterConsumer()
    {
        bool isRegistered = false;

        while (!isRegistered)
        {
            try
            {
                if (channel == null || channel.IsClosed)
                {
                    logger.LogTrace("Reseting connection befor registering consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
                    await ResetConnectionAsync();
                }
                else
                {
                    logger.LogTrace("Connection and channel is operational for registering consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
                }
                // don't declare, just check if exists
                await channel!.QueueDeclarePassiveAsync(queue: QConfig.Name!);

                // BasicQos configuration setting from config read.
                await channel.BasicQosAsync(QConfig.PrefetchSize ?? 0, QConfig.PrefetchCount ?? 0, global: false);
                isRegistered = true;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error registering consumer. Retrying in 5 seconds.");
                // TODO: should be configurable. Could this take advantage of a Backoff strategy?
                Task.Delay(5000).Wait();
                await ResetConnectionAsync();
            }
        }


        var consumer = new AsyncEventingBasicConsumer(channel!);

        if (_callback == null)
        {
            throw new IncorrectInitializationException("Callback not registered. Check your implementation.");
        }
        consumer.ReceivedAsync += GetAsyncListener(_callback);

        // If consumer already exists, it will throw an error. Channel will be closed and recreated.
        try
        {
            logger.LogTrace("Subscribing consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
            await channel!.BasicConsumeAsync(queue: QConfig.Name!,
                                autoAck: false,
                                consumer: consumer,
                                consumerTag: ConsumerTag);
        }
        catch (OperationInterruptedException e)
        {
            logger.LogWarning(e, "Consumer already registered. Closing channel and recreating. " + 
                "This has happened probably because there was a connection interruption that has been restablished.");
            channel!.Dispose();
            channel = await connection!.CreateChannelAsync();
            await channel.BasicQosAsync(QConfig.PrefetchSize ?? 0, QConfig.PrefetchCount ?? 0, global: false);
            await channel.BasicConsumeAsync(queue: QConfig.Name!,
                                autoAck: false,
                                consumer: consumer,
                                consumerTag: ConsumerTag);
        }

        logger.LogInformation("Waiting for messages from queue {messageOrigin}.", QConfig.Name);
    }

    protected override async Task _RegisterAsync(OnMessageCallback<T> callback)
    {
        _callback = callback;
        await RegisterConsumer();
    }

    /// <inheritdoc/>
    public override async Task UnSubscribeAsync()
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Unsubscribing consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
        }
        // this throws an null pointer exception if the consumer has never registered before.
        if (channel == null || !channel.IsOpen)
        {
            logger.LogWarning("Channel is not open. Cannot unsubscribe consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
            return;
        }
        await channel!.BasicCancelAsync(ConsumerTag);
    }

    /// <summary>
    /// Use close channel to stop receiving messages and close the channel.
    /// If you are looking to control message consumption, consider using UnSubscribe.
    /// </summary>
    public async Task CloseChannel()
    {
        logger.LogTrace("Close channel initiated by consumer '{Tag}' for queue '{Queue}'.", ConsumerTag, QConfig.Name);
        // For Reply Codes, refer to https://www.rfc-editor.org/rfc/rfc2821#page-42
        await channel!.CloseAsync(421, "Consumer temporarily unavailable.");
    }

    public AsyncEventHandler<BasicDeliverEventArgs> GetAsyncListener(OnMessageCallback<T> callback)
    {
        return async (ch, ea) =>
        {
            try
            {
                logger.LogTrace(" [Receiving-Thread:{ThreadID}] Message with delivery tag: {Tag}", Environment.CurrentManagedThreadId, ea.DeliveryTag);

                var body = ea.Body.ToArray();
                var message = JsonSerializer.Deserialize<T>(body);

                var success = await callback(message);
                if (success)
                {
                    logger.LogTrace(" [Acknwolledging] Message with delivery tag: {Tag}", ea.DeliveryTag);
                    await channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                else
                {
                    logger.LogTrace(" [Rejecting and requeing] Message with delivery tag: {Tag}", ea.DeliveryTag);
                    await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            }
            catch (JsonException e)
            {
                // Deserialization exception is a final exception and removes the message from the queue.
                logger.LogError(e, "Discarding unparseable message with id: {messageId} and tag: {Tag}", ea.BasicProperties.MessageId, ea.DeliveryTag);
                await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (UnauthorizedAccessException e)
            {
                // Unauthorized exception is a final exception and removes the message from the queue hopefully to a DLQ.
                logger.LogError(e, "Unacking unauthorized message with tag '{Tag}' with requeue set to false. Hope you have DLQ set.", ea.DeliveryTag);
                await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception e)
            {
                logger.LogError(
                    e,
                    "Got an error from Callback handler function for message with tag: {Tag}. Message will be requeued until dead letter policy takes action.",
                    ea.DeliveryTag);
                await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
            finally
            {   
                logger.LogTrace(" [waiting]");
            }
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

    /// <inheritdoc/>
    public override async Task<IHealthReport> ReportHealthStatusAsync()
    {
        return await Task.Run(() =>
        {
            var status = IsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            
            var report = new HealthReport(GetType().Name, status)
            {
                Description = $"RabbitMQ Connection Status: {status}"
            };
            return report;

        });
    }
}

