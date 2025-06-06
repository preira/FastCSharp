using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FastCSharp.RabbitPublisher.Common;

public class InjectableRabbitConnectionPool : RabbitConnectionPool
{
    public InjectableRabbitConnectionPool(IOptions<RabbitPublisherConfig> config, ILoggerFactory loggerFactory)
        : base(config.Value, loggerFactory)
    {
    }
}
