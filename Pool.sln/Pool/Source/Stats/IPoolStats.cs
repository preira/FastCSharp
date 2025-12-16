using System.Text.Json;

namespace FastCSharp.Pool;

public interface IPoolStats
{
    public long RequestCount { get; }
    public double HitRatio { get; }
    public double ReturnRatio { get; }
    public double ErrorRatio { get; }
    public double PurgeRatio { get; }
    public double WaitRatio { get; }
    public double TimeoutRatio { get; }
    public double DisposedRatio { get; }
    public double SizeChangeEventsRatio { get; }
    public double SizeChangeEventsPerMinute { get; }
    public double MaxSize { get; }
    public double MinSize { get; }
    public DateTime PeriodStart { get; }
    public JsonDocument ToJson();
}
