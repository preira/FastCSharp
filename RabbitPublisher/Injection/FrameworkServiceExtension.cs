using Microsoft.Extensions.Configuration;
using FastCSharp.RabbitPublisher.Common;
using Microsoft.Extensions.Options;
using FastCSharp.RabbitPublisher.Injection;
using FastCSharp.Publisher;
using FastCSharp.RabbitPublisher.Impl;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding dependencies to the service collection.
/// </summary>
public static class FrameworkServiceExtension
{
    public static void AddRabbitPublisher<TMessage>(this IServiceCollection services, IConfiguration configuration)
    {
        // TO THINK: for having a configuration per message type consider:
        // services.AddSingleton<IOptions<RabbitPublisherConfig<TMessage>>>

        // we must make sure that there in only one configuration per application
        if (services.All(s => s.ServiceType != typeof(IOptions<RabbitPublisherConfig>)))
        {
            services.AddSingleton<IOptions<RabbitPublisherConfig>>(sp => {
                var section = configuration.GetSection(RabbitOptions.SectionName);
                RabbitOptions options = new();
                section.Bind(options.Value);
                return options;
            });
            AddConnecionPool(services);
        }    
        AddRabbitPublisher<TMessage>(services);
    }

    public static void AddRabbitPublisher<TMessage>(this IServiceCollection services, string file)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(file, true, true)
            .Build();

        services.AddRabbitPublisher<TMessage>(configuration);
    }

    private static void AddConnecionPool(IServiceCollection services)
    {
        // we must make that there is only one connection pool per application
        if (services.All(s => s.ServiceType != typeof(IRabbitConnectionPool)))
        {
            services.AddSingleton<IRabbitConnectionPool, InjectableRabbitConnectionPool>();

            // Force initialization of the connection pool
            var pool = services.BuildServiceProvider().GetRequiredService<IRabbitConnectionPool>();
        }
    }
    private static void AddRabbitPublisher<TMessage>(IServiceCollection services)
    {
        services.AddScoped<IAsyncPublisher<TMessage>, AsyncRabbitPublisher<TMessage>>();
    }
}