using FastCSharp.RabbitCommon;

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

    public virtual Dictionary<string, QueueConfig> Queues { get; internal set; } = new();

}
