using FastCSharp.RabbitCommon;

namespace FastCSharp.RabbitPublisher.Common;

public class RabbitPublisherConfig : RabbitConfig
{
    public TimeSpan Timeout { get; set; }
    public Dictionary<string, ExchangeConfig?>? Exchanges { get; set; }
}
