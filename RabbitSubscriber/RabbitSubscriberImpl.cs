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
using FastCSharp.Logging;

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
            isConnected = await Connect();
        }

    }

    private async Task<bool> Connect()
    {
        bool isConnected = false;
        try
        {
            if (connection == null || !connection.IsOpen)
            {
                channel?.Dispose();
                connection?.Dispose();

                if (endpoints == null)
                {
                    connection = await connectionFactory.CreateConnectionAsync();
                }
                else
                {
                    connection = await connectionFactory.CreateConnectionAsync(endpoints);
                }
                connection.ConnectionShutdownAsync += ShutdownEventHandler("Connection");

                channel = await connection.CreateChannelAsync();
                channel.ChannelShutdownAsync += ShutdownEventHandler("channel");
            }
            else if (channel == null || channel.IsClosed)
            {
                channel?.Dispose();
                channel = await connection.CreateChannelAsync();
            }

            isConnected = connection.IsOpen && channel.IsOpen;
        }
        catch (Exception e)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(e, "Error connecting to Rabbit MQ. Retrying in 5 seconds.");
            }
            // TODO: should be configurable. Could this take advantage of a Backoff strategy?
            await Task.Delay(5000);
        }

        return isConnected;
    }

    private AsyncEventHandler<ShutdownEventArgs> ShutdownEventHandler(string subcjet)
    {
        return async (sender, args) =>
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("RabbitMQ {Subject} shutdown: {Message}. FastCSharp Client will NOT try to recover. You should have a watch dog to handle reconnection.", subcjet, args.ReplyText);
            }
            await Task.Yield();
        };
    }

    /// <inheritdoc/>
    public override async Task ResetAsync()
    {
        await ResetConnectionAsync();
        await RegisterConsumer();
    }

    private async Task RegisterConsumer()
    {
        await VerifyChannel();

        var consumer = new AsyncEventingBasicConsumer(channel!);

        if (_callback == null)
        {
            throw new IncorrectInitializationException("Callback not registered. Check your implementation.");
        }
        consumer.ReceivedAsync += GetAsyncListener(_callback);

        // If consumer already exists, it will throw an error. Channel will be closed and recreated.
        try
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Subscribing consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
            }
            await channel!.BasicConsumeAsync(queue: QConfig.Name!,
                                    autoAck: false,
                                    consumer: consumer,
                                    consumerTag: ConsumerTag);
        }
        catch (OperationInterruptedException e)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogWarning(e, "Consumer already registered. Closing channel and recreating. " +
                    "This has happened probably because there was a connection interruption that has been restablished.");
            }
            channel!.Dispose();
            channel = await connection!.CreateChannelAsync();
            await channel.BasicQosAsync(QConfig.PrefetchSize ?? 0, QConfig.PrefetchCount ?? 0, global: false);
            await channel.BasicConsumeAsync(queue: QConfig.Name!,
                                autoAck: false,
                                consumer: consumer,
                                consumerTag: ConsumerTag);
        }
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Waiting for messages from queue {messageOrigin}.", QConfig.Name);
        }
    }

    private async Task VerifyChannel()
    {
        bool isRegistered = false;

        while (!isRegistered)
        {
            try
            {
                if (channel == null || channel.IsClosed)
                {
                    logger.Log(LogLevel.Trace)("Channel is closed or null. Attempting to reset connection before registering consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
                    await ResetConnectionAsync();
                }
                else
                {
                    logger.Log(LogLevel.Trace)("Connection and channel is operational for registering consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
                }
                // don't declare, just check if exists
                await channel!.QueueDeclarePassiveAsync(queue: QConfig.Name!);

                // BasicQos configuration setting from config read.
                await channel.BasicQosAsync(QConfig.PrefetchSize ?? 0, QConfig.PrefetchCount ?? 0, global: false);
                isRegistered = true;
            }
            catch (Exception e)
            {
                logger.LogException(LogLevel.Error)(e, "Error verifying channel for consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);

                // TODO: should be configurable. Could this take advantage of a Backoff strategy?
                Task.Delay(5000).Wait();
                await ResetConnectionAsync();
            }
        }
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
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("Channel is not open. Cannot unsubscribe consumer '{Tag}' from queue '{Queue}'.", ConsumerTag, QConfig.Name);
            }
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
        logger.Log(LogLevel.Trace)("Close channel initiated by consumer '{Tag}' for queue '{Queue}'.", ConsumerTag, QConfig.Name);
        // For Reply Codes, refer to https://www.rfc-editor.org/rfc/rfc2821#page-42
        await channel!.CloseAsync(421, "Consumer temporarily unavailable.");
    }

    public AsyncEventHandler<BasicDeliverEventArgs> GetAsyncListener(OnMessageCallback<T> callback)
    {
        return async (ch, ea) =>
        {
            try
            {
                logger.Log(LogLevel.Trace)(" [Received-Thread:{ThreadID}] Message with delivery tag: {Tag}", Environment.CurrentManagedThreadId, ea.DeliveryTag);

                var body = ea.Body.ToArray();
                var message = JsonSerializer.Deserialize<T>(body);

                var success = await callback(message);
                if (success)
                {
                    logger.Log(LogLevel.Trace)(" [Processing] Message with delivery tag: {Tag}", ea.DeliveryTag);
                    await channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                else
                {
                    logger.Log(LogLevel.Trace)(" [Rejecting and requeing] Message with delivery tag: {Tag}", ea.DeliveryTag);
                    await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            }
            catch (JsonException e)
            {
                // Deserialization exception is a final exception and removes the message from the queue.
                logger.LogException(LogLevel.Error)(e, "Discarding unparseable message with id: {messageId} and tag: {Tag}", ea.BasicProperties.MessageId, ea.DeliveryTag);
                await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (UnauthorizedAccessException e)
            {
                // Unauthorized exception is a final exception and removes the message from the queue hopefully to a DLQ.
                logger.LogException(LogLevel.Error)(e, "Unacking unauthorized message with tag '{Tag}' with requeue set to false. Hope you have DLQ set.", ea.DeliveryTag);
                await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception e)
            {
                // Any other exception is considered a transient error and the message will be requeued.
                logger.LogException(LogLevel.Error)(e, "Callback error processing message with tag: {Tag}. Message will be requeued until dead letter policy takes action.", ea.DeliveryTag);
                await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
            finally
            {   
                logger.LogTrace(" [waiting]");
            }
        };
    }

    // private delegate void LogExceptionDelegate(Exception? exception, string? message, params object?[] args);
    // private LogExceptionDelegate logger.LogException(LogLevel level)
    // {
    //     if (logger.IsEnabled(level))
    //     {
    //         switch (level)
    //         {
    //             case LogLevel.Critical:
    //                 return logger.LogCritical;
    //             case LogLevel.Error:
    //                 return logger.LogError;
    //             case LogLevel.Information:
    //                 return logger.LogInformation;
    //             case LogLevel.Trace:
    //                 return logger.LogTrace;
    //             case LogLevel.Warning:
    //                 return logger.LogWarning;
    //             default:
    //                 return logger.LogDebug;
    //         }
    //     }
    //     return (Exception? exception, string? message, params object?[] args) => {  }; // No-op if logging is not enabled for the level.
    // }

    // private delegate void LogDelegate(string? message, params object?[] args);
    // private LogDelegate logger.Log(LogLevel level)
    // {
    //     if (logger.IsEnabled(level))
    //     {
    //         switch (level)
    //         {
    //             case LogLevel.Critical:
    //                 return logger.LogCritical;
    //             case LogLevel.Error:
    //                 return logger.LogError;
    //             case LogLevel.Information:
    //                 return logger.LogInformation;
    //             case LogLevel.Trace:
    //                 return logger.LogTrace;
    //             case LogLevel.Warning:
    //                 return logger.LogWarning;
    //             default:
    //                 return logger.LogDebug;
    //         }
    //     }
    //     return (string? message, params object?[] args) => {  }; // No-op if logging is not enabled for the level.
    // }

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

