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
/// RabbitMQ Subscriber Factory. Provides a subscriber for a given queue.
/// </summary>
public class RabbitSubscriberFactory : ISubscriberFactory
{
    readonly private ConnectionFactory connectionFactory;
    readonly private RabbitSubscriberConfig config = new();
    // readonly private IDictionary<string, QueueConfig> queues = new Dictionary<string, QueueConfig>();
    readonly private ILoggerFactory loggerFactory;

    /// <summary>
    /// Creates a new RabbitMQ Subscriber Factory given a configuration and a logger factory.
    /// </summary>
    /// <param name="configuration">Rabbit Subscriber Configuration</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>A newlly creeated and configured RabbitMQSubscriberFactory</returns>
    public RabbitSubscriberFactory(IConfiguration configuration, ILoggerFactory loggerFactory) : base()
    {
        this.loggerFactory = loggerFactory;
        configuration.GetSection(nameof(RabbitSubscriberConfig)).Bind(config);

        connectionFactory = Configure();
    }

    /// <summary>
    /// Creates a new RabbitMQ Subscriber Factory given a configuration and a logger factory.
    /// </summary>
    /// <param name="configuration">Rabbit Subscriber Configuration</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>A newlly creeated and configured RabbitMQSubscriberFactory</returns>
    public RabbitSubscriberFactory(RabbitSubscriberConfig config, ILoggerFactory loggerFactory) : base()
    {
        this.loggerFactory = loggerFactory;

        this.config = config;

        connectionFactory = Configure();
    }

    private ConnectionFactory Configure()
    {
        ConnectionFactory connectionFactory;
        if ((config.HostName == null || config.Port == 0) && config.Hosts == null)
        {
            throw new IncorrectInitializationException(
                $"Message Queue was configured with Hostname:'{config.HostName}', Port:'{config.Port}', enpoints: '{JsonSerializer.Serialize(config.Hosts)}'.");
        }

        connectionFactory = new ConnectionFactory
        {
            ClientProvidedName = config.ClientName ?? "FastCSharp.RabbitMQSubscriber",
            NetworkRecoveryInterval = config.NetworkRecoveryInterval ?? TimeSpan.FromSeconds(10),
            AutomaticRecoveryEnabled = config.AutomaticRecoveryEnabled
        };

        if (config.HostName != null) connectionFactory.HostName = config.HostName;
        if (config.Port != null) connectionFactory.Port = (int)config.Port;
        if (config.VirtualHost != null) connectionFactory.VirtualHost = config.VirtualHost;
        if (config.Password != null) connectionFactory.Password = config.Password;

        if (config.UserName != null) connectionFactory.UserName = config.UserName;
        if (config.Heartbeat != null) connectionFactory.RequestedHeartbeat = (TimeSpan)config.Heartbeat;
        if (config.ChannelMax != null) connectionFactory.RequestedChannelMax = (ushort)config.ChannelMax;

        if (config.Queues.Count == 0)
        {
            throw new IncorrectInitializationException($"Message Queue was configured with no queues.");
        }

        return connectionFactory;
    }


    public ISubscriber<T> NewSubscriber<T>(string messageOrigin)
    {
        try
        {
            QueueConfig? queue = config.Queues[messageOrigin];

            if (queue == null || queue.Name == null || queue.Name == string.Empty)
            {
                throw new ArgumentException($"Could not find the queue for '{messageOrigin}' in the section {nameof(RabbitSubscriberConfig)}. Please check your configuration.");
            }

            return new RabbitSubscriber<T>(connectionFactory, queue, loggerFactory, config.Hosts);
        }
        catch (KeyNotFoundException)
        {
            throw new ArgumentException($"Could not find the queue for '{messageOrigin}' in the section {nameof(RabbitSubscriberConfig)}. Please check your configuration.");
        }
    }
}