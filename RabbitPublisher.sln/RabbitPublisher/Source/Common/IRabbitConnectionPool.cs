using FastCSharp.RabbitCommon;
using FastCSharp.Pool;
using FastCSharp.Observability;
using System.Text.Json;

namespace FastCSharp.RabbitPublisher.Common;

public interface IRabbitConnectionPool : IDisposable, IHealthReporter
{
    Task<IRabbitConnection> GetConnectionAsync(object owner);

    IPoolStats? Stats { get; }
    JsonDocument? FullStatsReport { get; }
}
