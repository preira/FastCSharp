using System.Collections.Immutable;

namespace FastCSharp.Observability;

public interface IHealthReport
{
    public ImmutableDictionary<string, IHealthReport> Dependencies { get; }

    public string Name { get; }

    public HealthStatus Status { get; }

    public string? Description { get; }

    public string ToString();

    public string Summarize();
}

