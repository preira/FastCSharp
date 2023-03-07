# FastCSharp's RabbitMQ Publisher  
This is the publisher implementation to connect to a RabbitMQ.  

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