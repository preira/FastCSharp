# FastCSharp's RabbitMQ Publisher  
RabbitPublisher provides a simple approach for publishing messages to a RabbitMQ exchange.  
It is a wrapper around the [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) library.

## Usage
```csharp
var exchange = new RabbitDirectExchangeFactory(configuration, loggerFactory);
using var publisher = exchange.NewPublisher<string>("PUBLISH.SDK.DIRECT", "TASK_QUEUE");
await publisher.Publish("Hello World!");
```


