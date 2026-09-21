using IncidentAgent.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IncidentAgent.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddInvestigationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(
            configuration.GetSection(PersistenceOptions.SectionName));

        var enabled = configuration.GetValue<bool>(
            $"{PersistenceOptions.SectionName}:Enabled");

        if (!enabled)
        {
            services.AddSingleton<IInvestigationStore, NullInvestigationStore>();
            return services;
        }

        var connectionString =
            configuration[$"{PersistenceOptions.SectionName}:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Persistence is enabled but no PostgreSQL connection string is configured.");
        }

        services.AddDbContext<InvestigationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IInvestigationStore, PostgresInvestigationStore>();

        return services;
    }

    public static async Task InitializeInvestigationPersistenceAsync(
        this IServiceProvider serviceProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>(
                $"{PersistenceOptions.SectionName}:Enabled"))
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext =
            scope.ServiceProvider.GetRequiredService<InvestigationDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
