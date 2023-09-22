using RabbitMQ.Client;

namespace FastCSharp.RabbitCommon;

public interface IFCSConnection
{
    bool IsOpen { get; }
    IModel CreateModel();
    bool ResetConnection(bool dispose = true);
    void Close();
    void Dispose();
}

/// <summary>
/// RabbitMQ Subscriber Configuration
/// </summary>
public class RabbitConfig
{
    public string? ClientName { get; set; }
    /// <summary>
    /// Hostname of the RabbitMQ server.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/uri-spec.html"/>
    /// <seealso href="https://www.rabbitmq.com/uri-query-parameters.html"/>
    public string? HostName { get; set; }
    /// <summary>
    /// Port of the RabbitMQ server.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/ports.html"/>
    /// <seealso href="https://www.rabbitmq.com/networking.html#tcp-ports"/>
    /// <seealso href="https://www.rabbitmq.com/ssl.html#tcp-ports"/>
    public int? Port { get; set; }
    /// <summary>
    /// Virtual host of the RabbitMQ server.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/uri-spec.html"/>
    /// <seealso href="https://www.rabbitmq.com/vhosts.html"/>
    public string? VirtualHost { get; set; }
    public IList<AmqpTcpEndpoint>? Hosts { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    /// <summary>
    /// Connection timeout in seconds. Default is 20 seconds.
    /// </summary>
    /// <seealso href="https://www.rabbitmq.com/connections.html#connection-timeout"/>
    /// <seealso href="https://www.rabbitmq.com/heartbeats.html"/>
    /// <seealso href="https://www.rabbitmq.com/networking.html#tcp-keepalive"/>
    public TimeSpan? Heartbeat { get; set; }

}



