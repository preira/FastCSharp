# FastCSharp's RabbitMQ Publisher  
RabbitPublisher provides a simple approach for publishing messages to a RabbitMQ exchange.  
It is a wrapper around the [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) library.

## Usage
All you need to do is create a new publisher to an existing exchange and publish a message.  
The example below shows how to publish a message to a direct exchange. Swagger is also configured to allow testing.  
The code needed to run is manly in the Runner class.  
### Program.cs
```csharp
using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// http://localhost:5106/swagger/v1/swagger.json
app.UseSwagger();
// http://localhost:5106/swagger
app.UseSwaggerUI();

var runner = new Runner<Message>();

// http://localhost:5106/SendDirectMessage?message=Hello%20World
app.MapGet("/SendDirectMessage", async Task<IResult> (string? message) => {
        var msg = new Message();
        msg.Text = message;
        await runner.Run(msg);
        return TypedResults.Accepted("");
    })
    .WithOpenApi();

app.Run();

public class Runner<T>
{
    IPublisher<T> publisher;
    public Runner()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("rabbitsettings.json", true, true)
            .Build();
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        IPublisherFactory publisherFactory = new RabbitDirectExchangeFactory(configuration, loggerFactory);
        publisher = publisherFactory.NewPublisher<T>("TEST_EXCHANGE", "TEST_QUEUE");
        Console.WriteLine(">> Runner Initialized!");
    }

    public async Task Run(T message)
    {
        bool isSent = await publisher.Publish(message);
        if (!isSent)
        {
            throw new Exception(">> Message not sent!");
        }
        Console.WriteLine(">> Message Sent!");
    }
}

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
    "RabbitPublisherConfig" : {
        "HostName" : "localhost",
        "Port" : 5672,
        "UserName"  : "guest",
        "Password"  : "guest",
        "Timeout" : "00:00:10",
        "Exchanges" : 
        {
            "TEST_EXCHANGE" : {
                "Name" : "test.direct.exchange.v-1.0",
                "Type" : "direct",
                "NamedRoutingKeys" : {
                    "TEST_QUEUE" : "test.direct.q"
                }
            }
        }        
    }
}
```

