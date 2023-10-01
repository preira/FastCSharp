
using Microsoft.Extensions.Configuration;
using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.Options;
using FastCSharp.RabbitPublisher.Injection;
using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding dependencies to the service collection.
/// </summary>
public static class FrameworkServiceExtension
{
    public static void AddRabbitPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IOptions<RabbitPublisherConfig>>(sp => {
            var section = configuration.GetSection(RabbitOptions.SectionName);
            RabbitOptions options = new();
            section.Bind(options.Value);
            return options;
        });

        // TODO: these should be scoped so that they can be disposed of properly returning the connection to the pool
        services.AddScoped<IPublisherFactory<ITopicPublisher>, TopicPublisherFactory>();
        services.AddScoped<IPublisherFactory<IFanoutPublisher>, FanoutPublisherFactory>();
        services.AddScoped<IPublisherFactory<IDirectPublisher>, DirectPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<ITopicPublisher>, TopicBatchPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<IFanoutPublisher>, FanoutBatchPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<IDirectPublisher>, DirectBatchPublisherFactory>();

    }
}