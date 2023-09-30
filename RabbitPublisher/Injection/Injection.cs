
using Microsoft.Extensions.Configuration;
using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.Options;
using FastCSharp.RabbitPublisher.Injection;
using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Impl;
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
        services.AddScoped<IPublisherFactory<ITopicPublisher>, RabbitTopicPublisherFactory>();
        services.AddScoped<IPublisherFactory<IFanoutPublisher>, RabbitFanoutPublisherFactory>();
        services.AddScoped<IPublisherFactory<IDirectPublisher>, RabbitDirectPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<ITopicBatchPublisher>, RabbitTopicBatchPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<IFanoutBatchPublisher>, RabbitFanoutBatchPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<IDirectBatchPublisher>, RabbitDirectBatchPublisherFactory>();

    }
}