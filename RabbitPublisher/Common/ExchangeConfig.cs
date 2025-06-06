namespace FastCSharp.RabbitPublisher.Common;

public class ExchangeConfig
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public IDictionary<string, string?>? Queues { get; set; }
    public IList<string>? RoutingKeys { get; set; }
}
