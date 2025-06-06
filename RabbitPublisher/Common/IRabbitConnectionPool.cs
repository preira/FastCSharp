using FastCSharp.RabbitCommon;
using FastCSharp.Pool;
using FastCSharp.Observability;

namespace FastCSharp.RabbitPublisher.Common;

public interface IRabbitConnectionPool : IDisposable, IHealthReporter
{
    Task<IRabbitConnection> GetConnectionAsync(object owner);

    IPoolStats? Stats { get; }
}
