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


## appsettings.json config file sample

```json
{
    "RabbitSubscriberConfig" : 
    {
        "HostName"  : "localhost",
        "Port"      : 5672,
        "UserName"  : "guest",
        "Password"  : "guest",
        "HeartbeatTimeout"  : "00:00:20",
        "Queues"    :
        {
            "QUEUE_TOKEN"   : 
            {
                "Name":"queue.name",
                "PrefecthCount":1,
                "PrefecthSize":0
            },
            "TASK_QUEUE"    : 
            {
                "Name":"test.direct.q",
                "PrefecthCount":1,
                "PrefecthSize":0
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

