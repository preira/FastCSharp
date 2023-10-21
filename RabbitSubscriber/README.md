# FastCSharp's RabbitMQ Subscriber  
RabbitSubscriber provides a simple approach for subscribing to a RabbitMQ queue.  
It is a wrapper around the [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) library.

## Usage  
All you need to do is create a new subscriber to an existing queue and register a callback.  

### Minimal Example (Check out BasicSubscriber project at FastCSharp.TestRabbitImpl for more complex project examples)

Create a new minimal console project.
```Console
dotnet new console -o BasicSubscriber
cd .\BasicSubscriber\
dotnet add package FastCSharp.RabbitSubscriber
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Logging.Console
```

Open the `BasicSubscriber.csproj` file and add the following configuration.  
```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

Create a json file named `appsettings.json` and add the following configuration.  
```json
{
    "RabbitSubscriberConfig" : 
    {
        "HostName"  : "localhost",
        "Port"      : 5672,
        "VirtualHost": "test-vhost",
        "UserName"  : "guest",
        "Password"  : "guest",
        "HeartbeatTimeout"  : "00:00:20",
        "Queues"    :
        {
            "DIRECT_QUEUE"    : 
            {
                "Name":"test.direct.q",
                "PrefecthCount":1,
                "PrefecthSize":0
            }
        }
    }
}
```

Go to the Rabbit Management UI and create a new queue named `test.direct.q`. You may bind it to the `amq.direct` exchange if you want to publish messages with a Publisher.

Open the `Program.cs` file and replace the code with the one below.  
```csharp   
using FastCSharp.RabbitSubscriber;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("Program");
IConfiguration defaultConfiguration = new ConfigurationBuilder()
    .AddJsonFile("rabbitsettings.CLUSTER.json", true, true)
    .Build();

var subscriberFactory = new RabbitSubscriberFactory(defaultConfiguration, loggerFactory);
using var directSubscriber = subscriberFactory.NewSubscriber<string>("DIRECT_QUEUE");
directSubscriber.Register(async (message) =>
{
    logger.LogInformation($"Received {message}");
    return await Task.FromResult(true);
});

logger.LogInformation(" Press [enter] to exit.");
Console.ReadLine();
```
All set. Run the project:  
```Console
dotnet run
```

Now you can go to the Rabbit Management UI and publish a message to the `test.direct.q` queue. Remember to enclose the message in double quotes (e.g. `"Hello World"`).


## Adding a Circuit Breaker

Add the following packages to the project:
```Console
dotnet add package FastCSharp.CircuitBreaker
```

Open the `Program.cs` file and replace the code with the one below.  
```csharp
using FastCSharp.CircuitBreaker;
using FastCSharp.RabbitSubscriber;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("Program");
IConfiguration defaultConfiguration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", true, true)
    .Build();

var circuit = new EventDrivenCircuitBreaker(
    new ConsecutiveFailuresBreakerStrategy(
        5, 
        new FixedBackoff(new TimeSpan(0, 0, 0, 0, 5))));

var subscriberFactory = new RabbitSubscriberFactory(defaultConfiguration, loggerFactory);
using var directSubscriber = subscriberFactory.NewSubscriber<string>("DIRECT_QUEUE");

circuit.OnOpen += (sender) => directSubscriber.UnSubscribe();
circuit.OnReset += (sender) => directSubscriber.Reset();

directSubscriber.Register(async (message) =>
{
    return await circuit.Wrap(async () =>
    {
        logger.LogInformation($"Received {message}");
        return await Task.FromResult(true);
    });
});

logger.LogInformation(" Press [enter] to exit.");
Console.ReadLine();
```

### Breaking down subscriber flux control 

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

