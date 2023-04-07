# FastCSharp's RabbitMQ Subscriber  
RabbitSubscriber provides a simple approach for subscribing to a RabbitMQ queue.  
It is a wrapper around the [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) library.

## Usage
```csharp
var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
using var subscriber = exchange.NewSubscriber<string>("SUBSCRIBE.SDK.DIRECT", "TASK_QUEUE");
subscriber.Subscribe(async (message, cancellationToken) =>
{
    await Task.Delay(1000, cancellationToken);
    Console.WriteLine(message);
});
```


## appsettings.json config file

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

## Adding a Circuit Breaker

The subscriber can be stopped by calling:
```csharp 
subscriber.UnSubscribe();
```
This is a useful callback when a circuit breaker is triggered and the ```OnOpen``` or ```OnBreak``` event is fired.  

The subscriber can be reset by calling:
```csharp 
subscriber.Reset();
```
This is a useful callback when a circuit breaker is triggered and the ```OnReset``` event is fired.  


Check the FastCSharp.CircuitBreaker package for more information on circuit breakers.  

