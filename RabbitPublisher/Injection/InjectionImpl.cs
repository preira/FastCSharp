using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.Options;

namespace FastCSharp.RabbitPublisher.Injection;

public class RabbitOptions : IOptions<RabbitPublisherConfig>
{
    public static string SectionName { get; set; } = nameof(RabbitPublisherConfig);
    private readonly RabbitPublisherConfig value = new ();
    public RabbitPublisherConfig Value => value;
}

