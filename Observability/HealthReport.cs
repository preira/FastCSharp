using System.Collections.Immutable;
using System.Text;

namespace FastCSharp.Observability;
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Inexistent,
    Saturated
}

public interface IHealthReporter
{
    public Task<IHealthReport> ReportHealthStatus();
}

public interface IHealthReport
{
    public ImmutableDictionary<string, IHealthReport> Dependencies { get; }

    public string Name { get; }

    public HealthStatus Status { get; }

    public string? Description { get; }

    public string ToString();

    public string Summarize();
}

public class EmptyHealthReporter : IHealthReporter
{
    public string Name { get; }
    public string Message { get; }
    public EmptyHealthReporter(string name, string message)
    {
        this.Name = name;
        this.Message = message;
    }
    public Task<IHealthReport> ReportHealthStatus() => Task.FromResult((IHealthReport) new HealthReport(Name, HealthStatus.Inexistent, Message));
}

public class HealthReport : IHealthReport
{
    private Dictionary<string, IHealthReport> dependencies = new();
    public ImmutableDictionary<string, IHealthReport> Dependencies => dependencies.ToImmutableDictionary(x => x.Key, x => x.Value);

    public string Name { get; set; }

    public HealthStatus Status { get; set; }

    public string? Description { get; set; }

    public HealthReport(string name, HealthStatus status, string? description = null)
    {
        Name = name;
        Status = status;
        Description = description;
    }

    public void AddDependency(IHealthReport dependency)
    {
        // TODO: update status based on dependency status
        // TODO: Push to parent
        dependencies.Add(dependency.Name, dependency);
    }

    public override string ToString()
    {
        StringBuilder report = new();
        report.Append($"{{\"name\": \"{Name}\",");
        report.Append($"\"status\": \"{Status.ToString()}\",");
        report.Append($"\"description\": \"{Description}\",");
        report.Append($"\"dependencies\": [");
        foreach (var dependency in dependencies)
        {
            report.Append(dependency.Value.ToString());
        }
        report.Append("]}");
        return report.ToString();
    }

    public string Summarize()
    {
        // if all status including dependencies is healthy, return healthy. 
        // If any status is degraded, return degraded and add list of messages for the degraded statuses.

        if (Status == HealthStatus.Healthy && dependencies.All(x => x.Value.Status == HealthStatus.Healthy))
        {
            return HealthStatus.Healthy.ToString();
        }
        else
        {
            StringBuilder report = new();
            report.Append($"{{\"name\": \"{Name}\",");
            report.Append($"\"status\": \"{Status}\",");
            report.Append($"\"dependencies-count\": \"{dependencies.Count}\",");
            report.Append($"\"dependencies\": [");
            foreach (var dependency in dependencies)
            {
                report.Append(dependency.Value.Summarize());
            }
            report.Append("]}");
            return report.ToString();
        }
    }
}

