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
        services.Configure<LokiEvidenceOptions>(
            configuration.GetSection(LokiEvidenceOptions.SectionName));
        services.Configure<TempoEvidenceOptions>(
            configuration.GetSection(TempoEvidenceOptions.SectionName));
        services.Configure<PrometheusEvidenceOptions>(
            configuration.GetSection(PrometheusEvidenceOptions.SectionName));

        services.AddSingleton<IIncidentEvidenceSource, DemoDeploymentEvidenceSource>();

        RegisterGitHub(services, configuration);
        RegisterLoki(services, configuration);
        RegisterTempo(services, configuration);
        RegisterPrometheus(services, configuration);

        services.AddTransient<IIncidentInvestigator, IncidentInvestigator>();

        return services;
    }

    private static void RegisterGitHub(
        IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool>($"{GitHubEvidenceOptions.SectionName}:Enabled"))
        {
            services.AddHttpClient<IIncidentEvidenceSource, GitHubCommitEvidenceSource>();
        }
        else
        {
            services.AddSingleton<IIncidentEvidenceSource, DemoCommitEvidenceSource>();
        }
    }

    private static void RegisterLoki(
        IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool>($"{LokiEvidenceOptions.SectionName}:Enabled"))
        {
            services.AddHttpClient<IIncidentEvidenceSource, LokiLogEvidenceSource>();
        }
        else
        {
            services.AddSingleton<IIncidentEvidenceSource, DemoLogEvidenceSource>();
        }
    }

    private static void RegisterTempo(
        IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool>($"{TempoEvidenceOptions.SectionName}:Enabled"))
        {
            services.AddHttpClient<IIncidentEvidenceSource, TempoTraceEvidenceSource>();
        }
        else
        {
            services.AddSingleton<IIncidentEvidenceSource, DemoTraceEvidenceSource>();
        }
    }

    private static void RegisterPrometheus(
        IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool>($"{PrometheusEvidenceOptions.SectionName}:Enabled"))
        {
            services.AddHttpClient<IIncidentEvidenceSource, PrometheusMetricEvidenceSource>();
        }
        else
        {
            services.AddSingleton<IIncidentEvidenceSource, DemoMetricEvidenceSource>();
        }
    }
}
