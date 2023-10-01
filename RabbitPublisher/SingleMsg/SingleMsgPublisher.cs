﻿using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Common;
using FastCSharp.RabbitPublisher.Impl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FastCSharp.RabbitPublisher;

/// <summary>
/// Abstract factory for creating RabbitMQ publishers
/// </summary>
/// <typeparam name="T">The type of the publisher to create</typeparam>
public abstract class AbstractRabbitPublisherFactory<T> : AbstractRabbitExchangeFactory, IPublisherFactory<T>
{
    protected AbstractRabbitPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
    }
    protected AbstractRabbitPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory) 
        : base(configuration, ILoggerFactory)
    {
    }
    public abstract IPublisher<M> NewPublisher<M>(string destination, string? routingKey = null);
}

public class RabbitDirectPublisherFactory : AbstractRabbitPublisherFactory<IDirectPublisher>
{
    protected RabbitDirectPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
    }
    public RabbitDirectPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    { }
    public override IPublisher<M> NewPublisher<M>(string destination, string? routingKey = null)
    {
        if (routingKey == null)
        {
            throw new ArgumentException($"Cannot create a new Publisher without a Routing Key. Routing key is mandatory and should match the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your implementation and configuration.");
        }
        ExchangeConfig exchange = GetExchangeConfig(destination);
        var key = exchange.NamedRoutingKeys?[routingKey];
        if (key == null)
        {
            throw new ArgumentException($"Could not find the routing key for '{routingKey}' in the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }

        string exchangeName = Util.SafelyExtractExchageName(exchange, "direct");
        return new DirectRabbitPublisher<M>(
                            factory: connectionFactory,
                            ILoggerFactory: ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout,
                            routingKey: key
                            );
    }
}

public class RabbitFanoutPublisherFactory : AbstractRabbitPublisherFactory<IFanoutPublisher>
{
    protected RabbitFanoutPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
    }
    public RabbitFanoutPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    public override IPublisher<M> NewPublisher<M>(string destination, string? routingKey = null)
    {
        ExchangeConfig exchange = GetExchangeConfig(destination);
        string exchangeName = Util.SafelyExtractExchageName(exchange, "fanout");
        return new FanoutRabbitPublisher<M>(factory: connectionFactory,
                            ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout);
    }
}

public class RabbitTopicPublisherFactory : AbstractRabbitPublisherFactory<ITopicPublisher>
{
    protected RabbitTopicPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
    }
    public RabbitTopicPublisherFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    public override IPublisher<M> NewPublisher<M>(string destination, string? routingKey = null)
    {
        routingKey ??= "";
        ExchangeConfig exchange = GetExchangeConfig(destination);
        string exchangeName = Util.SafelyExtractExchageName(exchange, "topic");
        var isOk = exchange?.RoutingKeys?.Contains(routingKey) ?? false;
        if (routingKey == "" || isOk)
        {
            return new TopicRabbitPublisher<M>(factory: connectionFactory,
                                ILoggerFactory,
                                exchange: exchangeName,
                                timeout: config.Timeout,
                                routingKey: routingKey);
        }
        throw new KeyNotFoundException($"Could not find the routing key for '{routingKey}' in RoutingKeys of the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
    }
}
