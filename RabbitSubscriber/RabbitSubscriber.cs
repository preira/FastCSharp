using FastCSharp.Subscriber;
using FastCSharp.Exception;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FastCSharp.RabbitSubscriber.Impl;
using System.Text.Json;

namespace FastCSharp.RabbitSubscriber;

/// <summary>
/// RabbitMQ Subscriber Configuration
/// </summary>
public class RabbitSubscriberConfig
{
    /// <summary>
    /// Hostname of the RabbitMQ server.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/uri-spec.html"/>
    /// <seealso href="https://www.rabbitmq.com/uri-query-parameters.html"/>
    public string? HostName { get; set; }
    /// <summary>
    /// Virtual host of the RabbitMQ server.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/uri-spec.html"/>
    /// <seealso href="https://www.rabbitmq.com/vhosts.html"/>
    public string? VirtualHost { get; set; }
    /// <summary>
    /// Port of the RabbitMQ server.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/ports.html"/>
    /// <seealso href="https://www.rabbitmq.com/networking.html#tcp-ports"/>
    /// <seealso href="https://www.rabbitmq.com/ssl.html#tcp-ports"/>
    public int Port { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? ClientName { get; set; }
    /// <summary>
    /// Connection timeout in seconds. Default is 20 seconds.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/connections.html#connection-timeout"/>
    /// <seealso href="https://www.rabbitmq.com/heartbeats.html"/>
    /// <seealso href="https://www.rabbitmq.com/networking.html#tcp-keepalive"/>
    public TimeSpan? HeartbeatTimeout { get; set; }
    /// <summary>
    /// Maximum number of channels allowed on the connection. Default is 1.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/connections.html#channels"/>
    /// <seealso href="https://www.rabbitmq.com/channels.html"/>
    /// <seealso href="https://www.rabbitmq.com/consumer-prefetch.html"/>
    public int? ChannelMax { get; internal set; }

}

/// <summary>
/// RabbitMQ Subscriber Factory. Provides a subscriber for a given queue.
/// </summary>
public class RabbitSubscriberFactory : ISubscriberFactory
{
    readonly private ConnectionFactory connectionFactory;
    readonly private RabbitSubscriberConfig config = new();
    readonly private IDictionary<string, RabbitQueueConfig?> queues;
    readonly private ILoggerFactory _loggerFactory;

    /// <summary>
    /// Creates a new RabbitMQ Subscriber Factory given a configuration and a logger factory.
    /// </summary>
    /// <param name="configuration">Rabbit Subscriber Configuration</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>A newlly creeated and configured RabbitMQSubscriberFactory</returns>
    public RabbitSubscriberFactory(IConfiguration configuration, ILoggerFactory loggerFactory) : base()
    {
        _loggerFactory = loggerFactory;
        configuration.GetSection(nameof(RabbitSubscriberConfig)).Bind(config);

        if (config.HostName == null || config.Port == 0)
        {
            throw new IncorrectInitializationException($"Message Queue was configuration configured with Hostname:'{config.HostName}', Port:'{config.Port}'.");
        }

        connectionFactory = new ConnectionFactory
        {
            ClientProvidedName = config.ClientName ?? "FastCSharp.RabbitMQSubscriber",
            HostName = config.HostName,
            VirtualHost = config.VirtualHost ?? "/",
            Port = config.Port,
            Password = config.Password,
            UserName = config.UserName,
            RequestedHeartbeat = config.HeartbeatTimeout ?? TimeSpan.FromSeconds(20),
            RequestedChannelMax = (ushort)(config.ChannelMax ?? 1),
        };
        
        queues = configuration.GetSection("RabbitSubscriberConfig:Queues").GetChildren().ToDictionary(x => x.Key, x => x.Get<RabbitQueueConfig?>());
        if (queues.Count == 0)
        {
            throw new IncorrectInitializationException($"Message Queue was configuration configured with no queues.");
        }
    }

    public ISubscriber<T> NewSubscriber<T>(string messageOrigin)
    {
        try
        {
            RabbitQueueConfig? queue = queues[messageOrigin];

            if (queue == null || queue.Name == null || queue.Name == string.Empty)
            {
                throw new ArgumentException($"Could not find the queue for '{messageOrigin}' in the section {nameof(RabbitSubscriberConfig)}. Please check your configuration.");
            }

            return new RabbitSubscriber<T>(connectionFactory, queue, _loggerFactory);
        }
        catch (KeyNotFoundException)
        {
            throw new ArgumentException($"Could not find the queue for '{messageOrigin}' in the section {nameof(RabbitSubscriberConfig)}. Please check your configuration.");
        }
    }
}