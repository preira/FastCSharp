namespace FastCSharp.Observability;

public interface IHealthReporter
{
    /// <summary>
    /// Obtains a health report for the resource.
    /// </summary>
    /// <returns>IHealthReport with the state of the resource.</returns>
    public Task<IHealthReport> ReportHealthStatusAsync();
}

