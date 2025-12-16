using Microsoft.Extensions.Logging;
using FastCSharp.Publisher;
using System.Text.Json;
using FastCSharp.RabbitPublisher.Common;
using RabbitMQ.Client.Exceptions;
using FastCSharp.RabbitCommon;
using Microsoft.Extensions.Options;
using FastCSharp.Observability;

namespace FastCSharp.RabbitPublisher.Impl;

/// <summary>
/// RabbitPublisher represents a publisher that can publish messages to a RabbitMQ broker.
/// The RabbitPublisher is not thread safe, so it is recommended to create a new instance
/// for each thread.
/// </summary>
/// <typeparam name="T">The Type of the message object to send.</typeparam>
public class AsyncRabbitPublisher<T> : IAsyncPublisher<T>
{
    private string? routingKey;
    private string? queue;
    private readonly TimeSpan confirmTimeout;
    private RabbitPublisherConfig Config { get; set; }
    private ExchangeConfig? Exchange { get; set;}
    private readonly IRabbitConnectionPool pool;
    private readonly IList<Handler<T>>? handlers;
    private readonly ILogger logger;
    private bool disposed = false;

    public AsyncRabbitPublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        IOptions<RabbitPublisherConfig> config, 
        IList<Handler<T>>? handlers = null)
    :base()
    {
        logger = ILoggerFactory.CreateLogger<AsyncRabbitPublisher<T>>();
        Config = config.Value;
        confirmTimeout = config.Value.Timeout;
        pool = connectionPool;
        this.handlers = handlers;
    }

    public AsyncRabbitPublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        RabbitPublisherConfig config)
    :base()
    {
        logger = ILoggerFactory.CreateLogger<AsyncRabbitPublisher<T>>();
        Config = config;
        confirmTimeout = config.Timeout;
        pool = connectionPool;
    }

    public IAsyncPublisher<T> ForExchange(string exchange)
    {
        Exchange = Config?.Exchanges?[exchange];
        if (Exchange == null || string.IsNullOrWhiteSpace(Exchange.Name))
        {
            throw new ArgumentException($"Could not find the exchange for '{exchange}' in the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }

        // reset previous configuration
        queue = null;
        routingKey = null;        

        return this;
    }

    public IAsyncPublisher<T> ForQueue(string queue)
    {
        this.queue = Exchange?.Queues?[queue];
        if (this.queue == null)
        {
            throw new ArgumentException($"Could not find the queue for '{queue}' in the Queues of section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }
        return this;
    }

    public IAsyncPublisher<T> ForRouting(string key)
    {
        if(Exchange?.RoutingKeys?.Contains(key) ?? false)
        {
            routingKey = key;
            return this;
        }
        throw new ArgumentException($"Could not find the routing key for '{key}' in the RoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
    }

    /// <summary>
    /// Will publish the object passed as argument in JSon format, according to
    /// the underlaying implementation.
    /// </summary>
    /// <param name="message">The object to publish.</param>
    /// <returns>A Boolean future.</returns>
    public async Task<bool> PublishAsync(T? message)
    {
        return await PublishAsync(
            async () => {
                if(handlers != null)
                    foreach (var handler in handlers) message = await handler(message);
            },
            async (IRabbitChannel channel, CancellationToken cancellationToken) => {
                byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(message);
                await channel.BasicPublishAsync(this, jsonUtf8Bytes, cancellationToken);
            }
        );
    }

    /// <summary>
    /// Will publish the whole list of messages passed as argument and await for confirmation at the
    /// end of the batch.
    /// </summary>
    /// <param name="messages">The list of messages to publish.</param>
    /// <returns>A Boolean future that indicates if the whole batch has been published
    /// or if a problem occured.</returns>
    public async Task<bool> PublishAsync(IEnumerable<T> messages)
    {
        var msgList = new List<T?>();
        return await PublishAsync(
            async () => {
                foreach (var message in messages)
                {
                    var msg = message;
                    if (handlers != null)
                        foreach (var handler in handlers) msg = await handler(msg);
                    
                    msgList.Add(msg);
                }
                
            },
            async (IRabbitChannel channel, CancellationToken cancellationToken) => {
                foreach (var message in msgList)
                {
                    byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(message);

                    ulong? sequenceNumber = await channel.NextPublishSeqNoAsync(this);
                    await channel.BasicPublishAsync(this, jsonUtf8Bytes, cancellationToken);

                    if (logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace("Exchange='{exchange}', SequenceNumber='{seqNr}'",
                                        Exchange?.Name ?? "", sequenceNumber);
                    }

                }
            }
        );
    }

    private async Task<bool> PublishAsync(Func<Task> PreProcess, Func<IRabbitChannel, CancellationToken, Task> Send)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        await PreProcess();
        
        try
        {
            await using var connection = await pool.GetConnectionAsync(this);
            await using var channel = await connection.GetChannelAsync(this, Exchange?.Name ?? "", queue, routingKey);
            try
            {
                var cancellationToken = new CancellationTokenSource(confirmTimeout).Token;
                await Send(channel, cancellationToken);

                return true;
            }
            catch (AlreadyClosedException ace)
            {
                if (logger.IsEnabled(LogLevel.Error))
                {
                    // Log the error with the exception type and message
                    logger.LogError(ace, "[ERROR PUBLISHING: CHANNEL IS CLOSED] {Exception}: {Message}", ace.GetType().FullName, ace.Message);
                }
                channel.IsStalled = true;
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "[ERROR PUBLISHING] {Exception}: {Message}", ex.GetType().FullName, ex.Message);
            }
        }
        return false;
    }

    protected virtual void Dispose(bool disposing)
    {
        if(!disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                // DO NOT DISPOSE pool here, it is managed by the DI container
            }
            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task<IHealthReport> ReportHealthStatusAsync()
    {
        string name = GetType().Name;
        HealthReport report;
        await using (var connection = await pool.GetConnectionAsync(this))
        {

            if (connection == null)
            {
                report = new(name, HealthStatus.Unhealthy, $"{name} could not get a connection from the pool.");
            }
            else if (connection.IsOpen)
            {
                report = new(name, HealthStatus.Healthy);
            }
            else
            {
                report = new(name, HealthStatus.Unhealthy, $"{name} could not connect to the broker.");
            }

        }

        report.AddDependency(await pool.ReportHealthStatusAsync());
        
        return report;
    }
}