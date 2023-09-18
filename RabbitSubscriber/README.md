# FastCSharp's RabbitMQ Subscriber  
RabbitSubscriber provides a simple approach for subscribing to a RabbitMQ queue.  
It is a wrapper around the [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) library.

## Usage  
All you need to do is create a new subscriber to an existing queue and register a callback.  

### Program.cs
```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FastCSharp.RabbitSubscriber;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("rabbitsettings.json", true, true)
    .Build();
ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());


var exchange = new RabbitSubscriberFactory(configuration, loggerFactory);
using var subscriber = exchange.NewSubscriber<Message>("TASK_QUEUE");
subscriber.Register(async (message) =>
{
    Console.WriteLine($"Received {message?.Text}");
    return await Task.Run<bool>(()=>true);
});

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();


public class Message
{
    public Message()
    {
    }

    public string? Text { get; set; }
}
```


### rabbitsettings.json config file sample

```json
{
    "RabbitSubscriberConfig" : 
    {
        "HostName"  : "localhost",
        "VirtualHost" : "MyVirtualHost",
        "Port"      : 5672,
        "UserName"  : "guest",
        "Password"  : "guest",
        "HeartbeatTimeout"  : "00:00:20",
        "Queues"    :
        {
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
This is a useful callback when a circuit breaker is triggered and the ```OnOpen``` or ```OnBreak``` events are fired.  

The subscriber can be reset by calling:
```csharp 
subscriber.Reset();
```
This is a useful callback when a circuit breaker is triggered and the ```OnReset``` event is fired.  


Check the FastCSharp.CircuitBreaker package for more information on circuit breakers.  

