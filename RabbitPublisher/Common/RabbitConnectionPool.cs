using FastCSharp.RabbitCommon;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using FastCSharp.Pool;
using FastCSharp.Observability;

namespace FastCSharp.RabbitPublisher.Common;

public class RabbitConnectionPool : IRabbitConnectionPool
{
    private readonly string nameToken;
    static private int instanceCount = 0;
    readonly AsyncPool<RabbitConnection, IConnection> pool;
    readonly ILogger logger;

    public IPoolStats? Stats => pool?.Stats;

    public RabbitConnectionPool(
        RabbitPublisherConfig config, 
        ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<RabbitConnectionPool>();

        nameToken = config.ClientName ?? "FastCSharp.RabbitPublisher";
        var factory = new ConnectionFactory();

        if(config.HostName != null)     factory.HostName = config.HostName;
        if(config.Port != null)         factory.Port = (int) config.Port;
        if(config.VirtualHost != null)  factory.VirtualHost = config.VirtualHost;
        if(config.Password != null)     factory.Password = config.Password;
        if(config.UserName != null)     factory.UserName = config.UserName;
        if(config.Heartbeat != null)    factory.RequestedHeartbeat = (TimeSpan) config.Heartbeat;

        var poolConfig = PoolConfigOrDefaults(config.Pool);

        pool = new AsyncPool<RabbitConnection, IConnection>(
            async () => await CreateConnection(factory, config.Hosts, loggerFactory),
            poolConfig.MinSize, 
            poolConfig.MaxSize, 
            poolConfig.Initialize, 
            poolConfig.GatherStats, 
            poolConfig.DefaultWaitTimeout.TotalMilliseconds
        );
    }

    private string getName() => $"{nameToken}-{Interlocked.Increment(ref instanceCount).ToString("D6")}";

    static private PoolConfig PoolConfigOrDefaults(PoolConfig? fromConfig)
    {
        var poolConf = new PoolConfig
        {
            MinSize = fromConfig?.MinSize ?? 1,
            MaxSize = fromConfig?.MaxSize ?? 5,
            Initialize = fromConfig?.Initialize ?? false,
            GatherStats = fromConfig?.GatherStats ?? false,
            DefaultWaitTimeout = fromConfig?.DefaultWaitTimeout ?? TimeSpan.FromMilliseconds(100)
        };
        return poolConf;
    }

    private async Task<RabbitConnection> CreateConnection(ConnectionFactory factory, IList<AmqpTcpEndpoint>? endpoints, ILoggerFactory loggerFactory)
    {
        IConnection? connection;
        try
        {
            factory.ClientProvidedName = getName();
            logger.LogInformation($"Created RabbitConnection #{instanceCount}");
            if(endpoints == null)
            {
                connection = await factory.CreateConnectionAsync();
            }
            else
            {
                connection = await factory.CreateConnectionAsync(endpoints);
            }

            return new RabbitConnection(connection, loggerFactory);

        }
        catch (Exception ex)
        {

            var error = $"\tFactory URI: {factory.Uri}\n";
            error += $"\tFactory endpoint: {factory?.HostName}:{factory?.Port}\n";

            if(endpoints != null)
            {
                var i = 0;
                endpoints.ToList().ForEach(e =>
                    error += $"\tendpoint {++i}: {e}\n"
                );
            }
            var usr = factory?.UserName;
            string? staredUsr = usr != null ? $"{usr[..1]}******{usr[^2..]}" : null;

            var pw = factory?.Password;
            string? staredPw = pw != null ? $"{pw[..1]}******{pw[^2..]}" : null;

            error += $"\n> This may be due to incorrect authentication, or the server may be unreacheable.";
            error += $"\n> Please check your user ('{staredUsr}') and password ('{staredPw}').";

            if (endpoints != null)
            {
                error += $"\n> Also check your hostnames:";
                endpoints?.ToList().ForEach(e =>
                    error += $"\n\t'{e?.HostName}':'{e?.Port}'"
                );
            }
            error += "\n";
            logger.LogError("[CONFIG ERROR] {message}\n{error}\n", ex.Message, error);

            throw new Exception(error, ex);
        }
    }    

    public async Task<IRabbitConnection> GetConnectionAsync(object owner)
    {
        return await pool.BorrowAsync(owner);
    }

    public void Dispose()
    {
        pool.Dispose();
    }

    public Task<IHealthReport> ReportHealthStatusAsync()
    {
        return pool.ReportHealthStatusAsync();
    }
}