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
public class RabbitPublisher<T> : IPublisher<T>
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

    public RabbitPublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        IOptions<RabbitPublisherConfig> config, 
        IList<Handler<T>>? handlers = null)
    :base()
    {
        logger = ILoggerFactory.CreateLogger<RabbitPublisher<T>>();
        Config = config.Value;
        confirmTimeout = config.Value.Timeout;
        pool = connectionPool;
        this.handlers = handlers;
    }

    public RabbitPublisher(
        IRabbitConnectionPool connectionPool,
        ILoggerFactory ILoggerFactory,
        RabbitPublisherConfig config)
    :base()
    {
        logger = ILoggerFactory.CreateLogger<RabbitPublisher<T>>();
        Config = config;
        confirmTimeout = config.Timeout;
        pool = connectionPool;
    }

    public IPublisher<T> ForExchange(string destination)
    {
        Exchange = Config?.Exchanges?[destination];
        if (Exchange == null || string.IsNullOrWhiteSpace(Exchange.Name))
        {
            throw new ArgumentException($"Could not find the exchange for '{destination}' in the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }

        // reset previous configuration
        queue = null;
        routingKey = null;        

        return this;
    }

    public IPublisher<T> ForQueue(string q)
    {
        queue = Exchange?.Queues?[q];
        if (queue == null)
        {
            throw new ArgumentException($"Could not find the queue for '{q}' in the Queues of section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }
        return this;
    }

    public IPublisher<T> ForRouting(string key)
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
    public async Task<bool> Publish(T? message)
    {
        return await Publish(
            async () => {
                if(handlers != null)
                    foreach (var handler in handlers) message = await handler(message);
            },
            (IRabbitChannel channel) => {
                byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(message);
                channel.BasicPublish(this, null, jsonUtf8Bytes);
            }
        );
    }

    /// <summary>
    /// Will publish the whole list of messages passed as argument and await for confirmation at the
    /// end of the batch.
    /// </summary>
    /// <param name="msgs">The list of messages to publish.</param>
    /// <returns>A Boolean future that indicates if the whole batch has been published
    /// or if a problem occured.</returns>
    public async Task<bool> Publish(IEnumerable<T> msgs)
    {
        var messages = new List<T?>();
        return await Publish(
            async () => {
                foreach (var message in msgs)
                {
                    var msg = message;
                    if (handlers != null)
                        foreach (var handler in handlers) msg = await handler(msg);
                    
                    messages.Add(msg);
                }
                
            },
            (IRabbitChannel channel) => {
                foreach (var message in messages)
                {
                    byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(message);

                    ulong? sequenceNumber = channel.NextPublishSeqNo(this);
                    channel.BasicPublish(this, null, jsonUtf8Bytes);

                    logger.LogTrace("{\"Exchange\"=\"{exchange}\", \"SequenceNumber\"=\"{seqNr}\"}",
                                    Exchange?.Name, sequenceNumber);

                }
            }
        );
    }

    private async Task<bool> Publish(Func<Task> PreProcess, Action<IRabbitChannel> Send)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);

        await PreProcess();
        
        try
        {
            using var connection = pool.Connection(this);
            using var channel = connection.Channel(this, Exchange?.Name ?? "", queue, routingKey);
            try
            {
                Send(channel);

                channel?.WaitForConfirmsOrDie(this, confirmTimeout);
                return true;
            }
            catch (AlreadyClosedException ace)
            {
                logger.LogError("[ERROR PUBLISHING: CHANNEL IS CLOSED] {Exception}: {Message}", ace.GetType().FullName, ace.Message);
                logger.LogDebug(ace.StackTrace);
                channel.IsStalled = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError("[ERROR PUBLISHING] {Exception}: {Message}", ex.GetType().FullName, ex.Message);
            logger.LogDebug(ex.StackTrace);
        }
        return false;
    }

    protected void Dispose(bool disposing)
    {
        if(!disposed)
        {
            if(disposing)
            {
                // pool.Dispose();
            }
            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public async Task<IHealthReport> ReportHealthStatus()
    {
        string name = GetType().Name;
        HealthReport report;
        using (var connection = pool.Connection(this))
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

        report.AddDependency(await pool.ReportHealthStatus());
        
        return report;
    }
}