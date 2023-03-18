﻿using FastCSharp.Publisher;
using RabbitMQ.Client;
using FastCSharp.RabbitPublisher.Impl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FastCSharp.RabbitPublisher;

public class ExchangeConfig
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public IDictionary<string, string?>? NamedRoutingKeys { get; set; }
    public IList<string>? RoutingKeys { get; set; }
}
public class RabbitPublisherConfig
{
    public string? HostName { get; set; }
    public int Port { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public TimeSpan Timeout { get; set; }
    public Dictionary<string, ExchangeConfig?>? Exchanges { get; set; }
}

public abstract class AbstractRabbitExchangeFactory : IPublisherFactory
{
    protected RabbitPublisherConfig config = new();
    protected readonly IConnectionFactory connectionFactory;
    protected ILoggerFactory ILoggerFactory;
    protected AbstractRabbitExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    {
        this.ILoggerFactory = ILoggerFactory;
        configuration.GetSection(nameof(RabbitPublisherConfig)).Bind(config);

        connectionFactory = new ConnectionFactory
        {
            HostName = config.HostName,
            Port = config.Port,
            Password = config.Password,
            UserName = config.UserName,
        };
    }
    public IPublisher<T> NewPublisher<T>(string destination, string routingKey = "")
    {
        var exchange = config?.Exchanges?[destination];
        if (exchange == null || exchange.Name == null)
        {
            throw new ArgumentException($"Could not find the exchange for '{destination}' in the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }
        return _NewPublisher<T>(exchange, routingKey);
    }
    protected abstract IPublisher<T> _NewPublisher<T>(ExchangeConfig exchange, string routingKey);
}

public class RabbitDirectExchangeFactory : AbstractRabbitExchangeFactory
{
    public RabbitDirectExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    { }
    protected override IPublisher<T> _NewPublisher<T>(ExchangeConfig exchange, string routingKey)
    {
        var key = exchange.NamedRoutingKeys?[routingKey];
        if (key == null)
        {
            throw new ArgumentException($"Could not find the routing key for '{routingKey}' in the NamedRoutingKeys of section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
        }

        string exchangeName = Util.SafelyExtractExchageName(exchange, "direct");
        return new DirectRMQPublisher<T>(
                            factory: connectionFactory,
                            ILoggerFactory: ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout,
                            routingKey: key
                            );
    }
}

public class RabbitFanoutExchangeFactory : AbstractRabbitExchangeFactory
{
    public RabbitFanoutExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    protected override IPublisher<T> _NewPublisher<T>(ExchangeConfig exchange, string routingKey)
    {
        string exchangeName = Util.SafelyExtractExchageName(exchange, "fanout");
        return new FanoutRMQPublisher<T>(factory: connectionFactory,
                            ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout);
    }
}

public class RabbitTopicExchangeFactory : AbstractRabbitExchangeFactory
{
    public RabbitTopicExchangeFactory(IConfiguration configuration, ILoggerFactory ILoggerFactory)
    : base(configuration, ILoggerFactory)
    {
    }
    protected override IPublisher<T> _NewPublisher<T>(ExchangeConfig exchange, string routingKey)
    {
        string exchangeName = Util.SafelyExtractExchageName(exchange, "topic");
        if (routingKey != "")
        {
            bool isKO = true;
            if (exchange.RoutingKeys == null)
            {
                isKO = true;
            }
            else
            {
                isKO = !exchange.RoutingKeys.Contains(routingKey);
            }
            if (isKO)
            {
                throw new KeyNotFoundException($"Could not find the routing key for '{routingKey}' in RoutingKeys of the section {nameof(RabbitPublisherConfig)}. Please check your configuration.");
            }
        }
        return new TopicRMQPublisher<T>(factory: connectionFactory,
                            ILoggerFactory,
                            exchange: exchangeName,
                            timeout: config.Timeout,
                            routingKey: routingKey);
    }
}
internal static class Util
{
    internal static string SafelyExtractExchageName(ExchangeConfig exchange, string exchangeType)
    {
        if (exchange.Type?.ToLower() != exchangeType)
        {
            throw new ArgumentException($"There is a problem in your configuration. You are trying to use a {exchangeType} with a {exchange.Type} configuration.");
        }
        if (exchange.Name == null)
        {
            throw new ArgumentException($"There is a problem in your configuration. You are missing the exchange name.");
        }
        return exchange.Name;
    }
}