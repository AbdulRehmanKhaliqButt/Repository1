using IncidentAgent.Core;
using IncidentAgent.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IncidentAgent.Tests;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void AddIncidentInvestigation_WhenAllRealSourcesEnabled_RegistersEveryProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{GitHubEvidenceOptions.SectionName}:Enabled"] = "true",
                [$"{LokiEvidenceOptions.SectionName}:Enabled"] = "true",
                [$"{TempoEvidenceOptions.SectionName}:Enabled"] = "true",
                [$"{PrometheusEvidenceOptions.SectionName}:Enabled"] = "true",
                [$"{KubernetesEvidenceOptions.SectionName}:Enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddIncidentInvestigation(configuration);

        using var provider = services.BuildServiceProvider();
        var evidenceSources = provider.GetServices<IIncidentEvidenceSource>().ToArray();

        Assert.Contains(evidenceSources, source => source is KubernetesDeploymentEvidenceSource);
        Assert.Contains(evidenceSources, source => source is GitHubCommitEvidenceSource);
        Assert.Contains(evidenceSources, source => source is LokiLogEvidenceSource);
        Assert.Contains(evidenceSources, source => source is TempoTraceEvidenceSource);
        Assert.Contains(evidenceSources, source => source is PrometheusMetricEvidenceSource);
        Assert.DoesNotContain(evidenceSources, source => source is DemoDeploymentEvidenceSource);
        Assert.DoesNotContain(evidenceSources, source => source is DemoCommitEvidenceSource);
        Assert.DoesNotContain(evidenceSources, source => source is DemoLogEvidenceSource);
        Assert.DoesNotContain(evidenceSources, source => source is DemoTraceEvidenceSource);
        Assert.DoesNotContain(evidenceSources, source => source is DemoMetricEvidenceSource);
    }
}
