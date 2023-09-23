using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Common;
using FastCSharp.RabbitPublisher.Impl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FastCSharp.RabbitPublisher;

public abstract class AbstractRabbitBatchPublisherFactory : AbstractRabbitExchangeFactory, IBatchPublisherFactory
{
    protected AbstractRabbitBatchPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory) 
        : base(configuration, ILoggerFactory)
    {
    }
    public abstract IBatchPublisher<T> NewPublisher<T>(string destination, string? routingKey = null);
}
public class RabbitDirectBatchPublisherFactory : AbstractRabbitBatchPublisherFactory
{
    public RabbitDirectBatchPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    { }
    public override IBatchPublisher<T> NewPublisher<T>(string destination, string? routingKey = null)
    {
        if (routingKey == null)
        {
            throw new ArgumentException($"Cannot create a new Publisher without a Routing Key. Routing key is mandatory and should match the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your implementation and configuration.");
        }
        ExchangeConfig exchange = base._NewPublisher(destination);
        var key = exchange.NamedRoutingKeys?[routingKey];
        if (key == null)
        {
            throw new ArgumentException($"Could not find the routing key for '{routingKey}' in the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }

        string exchangeName = Util.SafelyExtractExchageName(exchange, "direct");
        return new DirectRabbitBatchPublisher<T>(
                            factory: connectionFactory,
                            ILoggerFactory: ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout,
                            routingKey: key
                            );
    }
}

public class RabbitFanoutBatchPublisherFactory : AbstractRabbitBatchPublisherFactory
{
    public RabbitFanoutBatchPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    public override IBatchPublisher<T> NewPublisher<T>(string destination, string? routingKey = null)
    {
        ExchangeConfig exchange = base._NewPublisher(destination);
        string exchangeName = Util.SafelyExtractExchageName(exchange, "fanout");
        return new FanoutRabbitBatchPublisher<T>(factory: connectionFactory,
                            ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout);
    }
}

public class RabbitTopicBatchPublisherFactory : AbstractRabbitBatchPublisherFactory
{
    public RabbitTopicBatchPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    public override IBatchPublisher<T> NewPublisher<T>(string destination, string? routingKey = null)
    {
        routingKey ??= "";
        ExchangeConfig exchange = base._NewPublisher(destination);
        string exchangeName = Util.SafelyExtractExchageName(exchange, "topic");
        var isOk = exchange?.RoutingKeys?.Contains(routingKey) ?? false;
        if (routingKey == "" || isOk)
        {
            return new TopicRabbitBatchPublisher<T>(factory: connectionFactory,
                                ILoggerFactory,
                                exchange: exchangeName,
                                timeout: config.Timeout,
                                routingKey: routingKey);
        }
        throw new KeyNotFoundException($"Could not find the routing key for '{routingKey}' in RoutingKeys of the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
    }
}
