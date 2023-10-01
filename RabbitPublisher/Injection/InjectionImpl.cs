using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FastCSharp.RabbitPublisher.Injection;

public class RabbitOptions : IOptions<RabbitPublisherConfig>
{
    public static string SectionName { get; set; } = nameof(RabbitPublisherConfig);
    private readonly RabbitPublisherConfig value = new ();
    public RabbitPublisherConfig Value => value;
}

public class DirectPublisherFactory : RabbitDirectPublisherFactory, IPublisherFactory<IDirectPublisher>
{
    public DirectPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
    : base(options, loggerFactory)
    { }
}
public class FanoutPublisherFactory : RabbitFanoutPublisherFactory, IPublisherFactory<IFanoutPublisher>
{
    public FanoutPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
    : base(options, loggerFactory)
    { }
}
public class TopicPublisherFactory : RabbitTopicPublisherFactory, IPublisherFactory<ITopicPublisher>
{
    public TopicPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
    : base(options, loggerFactory)
    { }
}

public class DirectBatchPublisherFactory : RabbitDirectBatchPublisherFactory, IBatchPublisherFactory<IDirectPublisher>
{
    public DirectBatchPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
    : base(options, loggerFactory)
    { }
}
public class FanoutBatchPublisherFactory : RabbitFanoutBatchPublisherFactory, IBatchPublisherFactory<IFanoutPublisher>
{
    public FanoutBatchPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
    : base(options, loggerFactory)
    { }
}
public class TopicBatchPublisherFactory : RabbitTopicBatchPublisherFactory, IBatchPublisherFactory<ITopicPublisher>
{
    public TopicBatchPublisherFactory(IOptions<RabbitPublisherConfig> options, ILoggerFactory loggerFactory)
    : base(options, loggerFactory)
    { }
}
