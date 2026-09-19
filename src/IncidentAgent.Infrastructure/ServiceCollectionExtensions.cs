using IncidentAgent.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IncidentAgent.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIncidentInvestigation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GitHubEvidenceOptions>(
            configuration.GetSection(GitHubEvidenceOptions.SectionName));

        services.AddSingleton<IIncidentEvidenceSource, DemoDeploymentEvidenceSource>();
        services.AddSingleton<IIncidentEvidenceSource, DemoMetricEvidenceSource>();
        services.AddSingleton<IIncidentEvidenceSource, DemoLogEvidenceSource>();
        services.AddSingleton<IIncidentEvidenceSource, DemoTraceEvidenceSource>();

        var useGitHub = configuration.GetValue<bool>(
            $"{GitHubEvidenceOptions.SectionName}:Enabled");

        if (useGitHub)
        {
            services.AddHttpClient<IIncidentEvidenceSource, GitHubCommitEvidenceSource>();
        }
        else
        {
            services.AddSingleton<IIncidentEvidenceSource, DemoCommitEvidenceSource>();
        }

        services.AddTransient<IIncidentInvestigator, IncidentInvestigator>();

        return services;
    }
}
