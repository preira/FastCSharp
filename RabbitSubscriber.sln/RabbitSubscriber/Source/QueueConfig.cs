using Microsoft.Extensions.Configuration;

namespace FastCSharp.RabbitSubscriber;

/// <summary>
/// RabbitMQ Queue Configuration.
/// </summary>
public class QueueConfig
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
    public ushort? PrefetchSize { get; set; }

    public IConfigurationSection? Options { get; set; }
}
