// filepath: c:\Users\João\Projetos\Fast\Observability.sln\Observability\Source\HealthReportTest.cs
namespace FastCSharp.Observability.Tests
{
    public class HealthReportTest
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var report = new HealthReport("service", HealthStatus.Healthy, "desc");
            Assert.Equal("service", report.Name);
            Assert.Equal(HealthStatus.Healthy, report.Status);
            Assert.Equal("desc", report.Description);
            Assert.Empty(report.Dependencies);
        }

        [Fact]
        public void AddDependency_AddsDependency()
        {
            var main = new HealthReport("main", HealthStatus.Healthy);
            var dep = new HealthReport("dep", HealthStatus.Degraded, "problem");
            main.AddDependency(dep);
            Assert.Single(main.Dependencies);
            Assert.True(main.Dependencies.ContainsKey("dep"));
            Assert.Equal(dep, main.Dependencies["dep"]);
        }

        [Fact]
        public void ToString_OutputsExpectedJson()
        {
            var main = new HealthReport("main", HealthStatus.Healthy, "desc");
            var dep = new HealthReport("dep", HealthStatus.Degraded, "problem");
            main.AddDependency(dep);
            var json = main.ToString();
            Assert.Contains("\"name\": \"main\"", json);
            Assert.Contains("\"status\": \"Healthy\"", json);
            Assert.Contains("\"description\": \"desc\"", json);
            Assert.Contains("\"dependencies\": [", json);
            Assert.Contains("\"name\": \"dep\"", json);
            Assert.Contains("\"status\": \"Degraded\"", json);
        }

        [Fact]
        public void Summarize_ReturnsHealthyIfAllHealthy()
        {
            var main = new HealthReport("main", HealthStatus.Healthy);
            var dep = new HealthReport("dep", HealthStatus.Healthy);
            main.AddDependency(dep);
            var summary = main.Summarize();
            Assert.Equal("Healthy", summary);
        }

        [Fact]
        public void Summarize_ReturnsJsonIfAnyDegraded()
        {
            var main = new HealthReport("main", HealthStatus.Healthy);
            var dep = new HealthReport("dep", HealthStatus.Degraded, "problem");
            main.AddDependency(dep);
            var summary = main.Summarize();
            Assert.Contains("\"name\": \"main\"", summary);
            Assert.Contains("\"status\": \"Healthy\"", summary);
            Assert.Contains("\"dependencies-count\": \"1\"", summary);
            Assert.Contains("\"name\": \"dep\"", summary);
            Assert.Contains("\"status\": \"Degraded\"", summary);
        }
    }

    public class EmptyHealthReporterTest
    {
        [Fact]
        public async Task ReportHealthStatusAsync_ReturnsHealthyReport()
        {
            var reporter = new EmptyHealthReporter("Name", "Message");
            var report = await reporter.ReportHealthStatusAsync();
            Assert.NotNull(report);
            Assert.Equal("Name", report.Name);
            Assert.Equal(HealthStatus.Inexistent, report.Status);
        }
    }
}