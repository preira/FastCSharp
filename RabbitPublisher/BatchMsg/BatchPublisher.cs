using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Common;
using FastCSharp.RabbitPublisher.Impl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FastCSharp.RabbitPublisher;

public abstract class AbstractRabbitBatchPublisherFactory<T> : AbstractRabbitExchangeFactory, IBatchPublisherFactory<T>
{
    protected AbstractRabbitBatchPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
    }
    protected AbstractRabbitBatchPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory) 
        : base(configuration, ILoggerFactory)
    {
    }
    public abstract IBatchPublisher<M> NewPublisher<M>(string destination, string? routingKey = null);
}
public class RabbitDirectBatchPublisherFactory : AbstractRabbitBatchPublisherFactory<IDirectPublisher>
{
    public RabbitDirectBatchPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
    }
    public RabbitDirectBatchPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    { }
    public override IBatchPublisher<M> NewPublisher<M>(string destination, string? routingKey = null)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);
        if (routingKey == null)
        {
            throw new ArgumentException($"Cannot create a new Publisher without a Routing Key. Routing key is mandatory and should match the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your implementation and configuration.");
        }
        ExchangeConfig exchange = base.GetExchangeConfig(destination);
        var key = exchange.NamedRoutingKeys?[routingKey];
        if (key == null)
        {
            throw new ArgumentException($"Could not find the routing key for '{routingKey}' in the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }

        string exchangeName = Util.SafelyExtractExchageName(exchange, "direct");
        return new DirectRabbitBatchPublisher<M>(
                            factory: connectionFactory,
                            ILoggerFactory: loggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout,
                            routingKey: key
                            );
    }
}

public class RabbitFanoutBatchPublisherFactory : AbstractRabbitBatchPublisherFactory<IFanoutPublisher>
{
    public RabbitFanoutBatchPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
    }
    public RabbitFanoutBatchPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    public override IBatchPublisher<M> NewPublisher<M>(string destination, string? routingKey = null)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);
        ExchangeConfig exchange = base.GetExchangeConfig(destination);
        string exchangeName = Util.SafelyExtractExchageName(exchange, "fanout");
        return new FanoutRabbitBatchPublisher<M>(factory: connectionFactory,
                            loggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout);
    }
}

public class RabbitTopicBatchPublisherFactory : AbstractRabbitBatchPublisherFactory<ITopicPublisher>
{
    public RabbitTopicBatchPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
    }
    public RabbitTopicBatchPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    public override IBatchPublisher<M> NewPublisher<M>(string destination, string? routingKey = null)
    {
        if(disposed) throw new ObjectDisposedException(GetType().FullName);
        routingKey ??= "";
        ExchangeConfig exchange = base.GetExchangeConfig(destination);
        string exchangeName = Util.SafelyExtractExchageName(exchange, "topic");
        var isOk = exchange?.RoutingKeys?.Contains(routingKey) ?? false;
        if (routingKey == "" || isOk)
        {
            return new TopicRabbitBatchPublisher<M>(factory: connectionFactory,
                                loggerFactory,
                                exchange: exchangeName,
                                timeout: config.Timeout,
                                routingKey: routingKey);
        }
        throw new KeyNotFoundException($"Could not find the routing key for '{routingKey}' in RoutingKeys of the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
    }
}
