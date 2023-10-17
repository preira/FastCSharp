using Microsoft.Extensions.Configuration;
using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.Options;
using FastCSharp.RabbitPublisher.Injection;
using FastCSharp.Publisher;

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

        AddRabbitPublisher(services);
    }

    public static void AddRabbitPublisher(this IServiceCollection services, string file)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(file, true, true)
            .Build();

        services.AddRabbitPublisher(configuration);
    }
    private static void AddRabbitPublisher(IServiceCollection services)
    {
        services.AddSingleton<IPublisherFactory<ITopicPublisher>, TopicPublisherFactory>();
        services.AddSingleton<IPublisherFactory<IFanoutPublisher>, FanoutPublisherFactory>();
        services.AddSingleton<IPublisherFactory<IDirectPublisher>, DirectPublisherFactory>();
        services.AddSingleton<IBatchPublisherFactory<ITopicPublisher>, TopicBatchPublisherFactory>();
        services.AddSingleton<IBatchPublisherFactory<IFanoutPublisher>, FanoutBatchPublisherFactory>();
        services.AddSingleton<IBatchPublisherFactory<IDirectPublisher>, DirectBatchPublisherFactory>();

    }
}