
namespace FastCSharp.Pool;

public class PoolConfig
{
    public int MinSize { get; set; }
    public int MaxSize { get; set; }
    public bool? Initialize { get; set; }
    public bool? GatherStats { get; set; }
    public TimeSpan? DefaultWaitTimeout { get; set; }

}