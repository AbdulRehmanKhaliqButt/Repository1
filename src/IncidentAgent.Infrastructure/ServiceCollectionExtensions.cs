using IncidentAgent.Core;
using Microsoft.Extensions.DependencyInjection;

namespace IncidentAgent.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIncidentInvestigationDemo(this IServiceCollection services)
    {
        services.AddSingleton<IIncidentEvidenceSource, DemoDeploymentEvidenceSource>();
        services.AddSingleton<IIncidentEvidenceSource, DemoCommitEvidenceSource>();
        services.AddSingleton<IIncidentEvidenceSource, DemoMetricEvidenceSource>();
        services.AddSingleton<IIncidentEvidenceSource, DemoLogEvidenceSource>();
        services.AddSingleton<IIncidentEvidenceSource, DemoTraceEvidenceSource>();
        services.AddSingleton<IIncidentInvestigator, IncidentInvestigator>();

        return services;
    }
}
