using FastCSharp.Subscriber;
using FastCSharp.Exceptions;
using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FastCSharp.RabbitSubscriber.Impl;
using System.Text.Json;

namespace FastCSharp.RabbitSubscriber;

/// <summary>
/// RabbitMQ Subscriber Configuration
/// </summary>
public class RabbitSubscriberConfig : RabbitConfig
{
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

        if ((config.HostName == null || config.Port == 0) && config.Hosts == null)
        {
            throw new IncorrectInitializationException(
                $"Message Queue was configuration configured with Hostname:'{config.HostName}', Port:'{config.Port}', enpoints: '{JsonSerializer.Serialize(config.Hosts)}'.");
        }


        connectionFactory = new ConnectionFactory
        {
            ClientProvidedName = config.ClientName ?? "FastCSharp.RabbitMQSubscriber"
        };

        if (config.HostName != null) connectionFactory.HostName = config.HostName;
        if(config.Port != null) connectionFactory.Port = (int) config.Port;
        if(config.VirtualHost != null) connectionFactory.VirtualHost = config.VirtualHost;
        if(config.Password != null) connectionFactory.Password = config.Password;
        if(config.UserName != null) connectionFactory.UserName = config.UserName;
        if(config.Heartbeat != null) connectionFactory.RequestedHeartbeat = (TimeSpan) config.Heartbeat;
        if(config.ChannelMax != null) connectionFactory.RequestedChannelMax = (ushort) config.ChannelMax;

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

            return new RabbitSubscriber<T>(connectionFactory, queue, _loggerFactory, config.Hosts);
        }
        catch (KeyNotFoundException)
        {
            throw new ArgumentException($"Could not find the queue for '{messageOrigin}' in the section {nameof(RabbitSubscriberConfig)}. Please check your configuration.");
        }
    }
}