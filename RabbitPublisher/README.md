# FastCSharp's RabbitMQ Publisher  
RabbitPublisher provides a simple approach for publishing messages to a RabbitMQ exchange.  
It is a wrapper around the [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) library.

## Batch Publishing
It includes an implementation for batch publishing. Send an IEnumerable of messages and they will be published in a single batch. This is useful when you need to publish a large number of messages with very high throughput.

## Usage
All you need to do is create a new publisher to an existing exchange and publish a message.  
The example below shows how to publish a message to a direct exchange. Swagger is also configured to allow testing.  
The code needed to run is manly in the Runner class.  
Checkout **FastCSharp.TestRabbitImpl** project for a more complete example.  

### Dependency injection Example (Check out BasicPublisher project at FastCSharp.TestRabbitImpl for full project)  
Create a new minimal API project.
```Console
dotnet new web -o BasicPublisher
cd .\BasicPublisher\
dotnet add package FastCSharp.RabbitPublisher
```
Add the following configuration to appsettings.json.  

```json
  "RabbitPublisherConfig" : {
    "Timeout" : "00:00:10",
    "Exchanges" : 
    {
        "DIRECT_EXCHANGE" : {
            "Name" : "amq.direct",
            "Type" : "direct"
        }
    }
  }
```
The file should look something like this.  

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "RabbitPublisherConfig" : {
    "Timeout" : "00:00:10",
    "Exchanges" : 
    {
        "DIRECT_EXCHANGE" : {
            "Name" : "amq.direct",
            "Type" : "direct"
        }
    }
  }
}
```

Open Program.cs and replace the code with the one below.  

```csharp
using FastCSharp.Publisher;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRabbitPublisher<string>(builder.Configuration);

var app = builder.Build();


app.MapGet("/", async (string message, IAsyncRabbitPublisher<string> publisher) => {
    return await publisher.ForExchange("DIRECT_EXCHANGE").PublishAsync(message);
});

app.Run();
```

Create a queue (e.g. named "test") in RabbitMQ and bin it to the exchange ```amq.direct```.  

Run the code.  
    
```Console
dotnet run
```

Execute the endpoint with a message visiting the created endpoint (replace the 5033 port for the one configured in your API).  
http://localhost:5033/?message=Hello%20World

That's it. You should see the message in the queue.

### Example using new (Check out BasicPublisher project at FastCSharp.TestRabbitImpl for full project)  
We will use the same configuration as the previous example.  

Create a new minimal API project.  
```Console
dotnet new web -o BasicPublisher
cd .\BasicPublisher\
dotnet add package FastCSharp.RabbitPublisher
```
Add the following configuration to appsettings.json.  

```json
  "RabbitPublisherConfig" : {
    "Timeout" : "00:00:10",
    "Exchanges" : 
    {
        "DIRECT_EXCHANGE" : {
            "Name" : "amq.direct",
            "Type" : "direct"
        }
    }
  }
```
The file should look something like this.  

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "RabbitPublisherConfig" : {
    "Timeout" : "00:00:10",
    "Exchanges" : 
    {
        "DIRECT_EXCHANGE" : {
            "Name" : "amq.direct",
            "Type" : "direct"
        }
    }
  }
}
```

Open Program.cs and replace the code with the one below.  

```csharp
using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Common;
using FastCSharp.RabbitPublisher.Impl;
using FastCSharp.RabbitPublisher.Injection;

var builder = WebApplication.CreateBuilder(args);

RabbitPublisherConfig config = new();
builder.Configuration.GetSection(RabbitOptions.SectionName).Bind(config);

ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

var connectionPool = new RabbitConnectionPool(config, loggerFactory);


var app = builder.Build();

app.MapGet("/", async (string message) => {
    IAsyncRabbitPublisher<string> publisher = new AsyncRabbitPublisher<string>(connectionPool, loggerFactory, config);
    return await publisher.ForExchange("DIRECT_EXCHANGE").PublishAsync(message);
});

app.Run();
```

Create a queue (e.g. named "test") in RabbitMQ and bin it to the exchange ```amq.direct```.  

Run the code.  
    
```Console
dotnet run
```

Execute the endpoint with a message visiting the created endpoint (replace the 5033 port for the one configured in your API).  
http://localhost:5033/?message=Hello%20World

That's it. You should see the message in the queue.

