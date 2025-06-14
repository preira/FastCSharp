// filepath: c:\Users\João\Projetos\Fast\RabbitPublisher.sln\RabbitPublisher\Source\AsyncRabbitPublisherTest.cs
using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FastCSharp.RabbitPublisher.Impl;
using FastCSharp.RabbitPublisher.Common;
using FastCSharp.RabbitCommon;
using FastCSharp.Observability;
using FastCSharp.Publisher;

namespace FastCSharp.RabbitPublisher.Tests;

public class AsyncRabbitPublisherTest
{
    private static RabbitPublisherConfig GetConfig() =>
        new RabbitPublisherConfig
        {
            Timeout = TimeSpan.FromSeconds(1),
            Exchanges = new Dictionary<string, ExchangeConfig?>
            {
                ["ex"] = new ExchangeConfig
                {
                    Name = "ex",
                    Queues = new Dictionary<string, string?> { ["q"] = "q" },
                    RoutingKeys = new List<string> { "rk" }
                }
            }
        };

    private static Mock<ILogger> MockLogger(LogLevel enabledLevel = LogLevel.None)
    {
        var logger = new Mock<ILogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns<LogLevel>(lvl => lvl >= enabledLevel);
        return logger;
    }

    [Fact]
    public void ForExchange_Valid_SetsExchange()
    {
        var pub = new AsyncRabbitPublisher<string>(
            Mock.Of<IRabbitConnectionPool>(),
            Mock.Of<ILoggerFactory>(),
            Options.Create(GetConfig())
        );
        var result = pub.ForExchange("ex");
        Assert.Same(pub, result);
    }

    [Fact]
    public void ForExchange_Invalid_Throws()
    {
        var pub = new AsyncRabbitPublisher<string>(
            Mock.Of<IRabbitConnectionPool>(),
            Mock.Of<ILoggerFactory>(),
            Options.Create(GetConfig())
        );
        Assert.Throws<KeyNotFoundException>(() => pub.ForExchange("invalid"));
    }

    [Fact]
    public void ForQueue_Valid_SetsQueue()
    {
        var pub = new AsyncRabbitPublisher<string>(
            Mock.Of<IRabbitConnectionPool>(),
            Mock.Of<ILoggerFactory>(),
            Options.Create(GetConfig())
        );
        pub.ForExchange("ex");
        var result = pub.ForQueue("q");
        Assert.Same(pub, result);
    }

    [Fact]
    public void ForQueue_Invalid_Throws()
    {
        var pub = new AsyncRabbitPublisher<string>(
            Mock.Of<IRabbitConnectionPool>(),
            Mock.Of<ILoggerFactory>(),
            Options.Create(GetConfig())
        );
        pub.ForExchange("ex");
        Assert.Throws<KeyNotFoundException>(() => pub.ForQueue("invalid"));
    }

    [Fact]
    public void ForRouting_Valid_SetsRoutingKey()
    {
        var pub = new AsyncRabbitPublisher<string>(
            Mock.Of<IRabbitConnectionPool>(),
            Mock.Of<ILoggerFactory>(),
            Options.Create(GetConfig())
        );
        pub.ForExchange("ex");
        var result = pub.ForRouting("rk");
        Assert.Same(pub, result);
    }

    [Fact]
    public void ForRouting_Invalid_Throws()
    {
        var pub = new AsyncRabbitPublisher<string>(
            Mock.Of<IRabbitConnectionPool>(),
            Mock.Of<ILoggerFactory>(),
            Options.Create(GetConfig())
        );
        pub.ForExchange("ex");
        Assert.Throws<ArgumentException>(() => pub.ForRouting("invalid"));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var pub = new AsyncRabbitPublisher<string>(
            Mock.Of<IRabbitConnectionPool>(),
            Mock.Of<ILoggerFactory>(),
            Options.Create(GetConfig())
        );
        pub.Dispose();
        pub.Dispose();
    }

}