namespace FastCSharp.Observability;

public class EmptyHealthReporter : IHealthReporter
{
    public string Name { get; }
    public string Message { get; }
    public EmptyHealthReporter(string name, string message)
    {
        this.Name = name;
        this.Message = message;
    }
    public Task<IHealthReport> ReportHealthStatusAsync() => Task.FromResult((IHealthReport) new HealthReport(Name, HealthStatus.Inexistent, Message));
}

