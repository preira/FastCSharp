# FastCSharp's RabbitMQ Publisher  
RabbitPublisher provides a simple approach for publishing messages to a RabbitMQ exchange.  
It is a wrapper around the [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) library.

## Usage
```csharp
var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
using var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE");
await publisher.Publish("Hello World!");
```

## appsettings.json config file sample

```json
{
    "RabbitPublisherConfig" : 
    {
        "HostName"  : "localhost",
        "Port"      : 5672,
        "UserName"  : "guest",
        "Password"  : "guest",
        "Timeout"   : "00:00:05",
        "Exchanges" : 
        {
            "PUBLISH.SDK.DIRECT" : 
            {
                "Name" : "test.direct.exchange",
                "Type" : "Direct",
                "NamedRoutingKeys": 
                {
                    "TASK_QUEUE" : "test.direct.q" 
                }
            },
            "PUBLISH.SDK.FANOUT" : 
            {
                "Name" : "test.fanout.exchange",
                "Type" : "Fanout"
            },
            "PUBLISH.SDK.TOPIC" : 
            {
                "Name" : "test.topic.exchange",
                "Type" : "Topic",
                "RoutingKeys" : [".mail.", ".sms.", ".letter"]
            }
        }
     }
}
```

