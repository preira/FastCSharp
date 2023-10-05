
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

        //TODO: create connection pool
        // var factory = new ConnectionFactory
        // {
        //     ClientProvidedName = config.ClientName ?? "FastCSharp.RabbitPublisher"
        // };

        // if (config.HostName != null) factory.HostName = config.HostName;
        // if(config.Port != null) factory.Port = (int) config.Port;
        // if(config.VirtualHost != null) factory.VirtualHost = config.VirtualHost;
        // if(config.Password != null) factory.Password = config.Password;
        // if(config.UserName != null) factory.UserName = config.UserName;
        // if(config.Heartbeat != null) factory.RequestedHeartbeat = (TimeSpan) config.Heartbeat;
        
        // return new RabbitConnection(factory, loggerFactory, config.Hosts);

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
        services.AddScoped<IPublisherFactory<ITopicPublisher>, TopicPublisherFactory>();
        services.AddScoped<IPublisherFactory<IFanoutPublisher>, FanoutPublisherFactory>();
        services.AddScoped<IPublisherFactory<IDirectPublisher>, DirectPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<ITopicPublisher>, TopicBatchPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<IFanoutPublisher>, FanoutBatchPublisherFactory>();
        services.AddScoped<IBatchPublisherFactory<IDirectPublisher>, DirectBatchPublisherFactory>();

    }
}