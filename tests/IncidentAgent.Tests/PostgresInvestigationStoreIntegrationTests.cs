using IncidentAgent.Core;
using IncidentAgent.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IncidentAgent.Tests;

public sealed class PostgresInvestigationStoreIntegrationTests
{
    [Fact]
    public async Task Store_RoundTripsFullInvestigation_AndSupportsHistoryFilters()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<InvestigationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new InvestigationDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();

        var store = new PostgresInvestigationStore(dbContext);

        var request = new IncidentRequest(
            "Checkout failures",
            "Checkout failures started after a deployment.",
            "payment-service",
            DateTimeOffset.Parse("2026-09-19T16:00:00Z"));

        var investigation = new IncidentInvestigation(
            "investigation-roundtrip-1",
            DateTimeOffset.Parse("2026-09-19T16:10:00Z"),
            "Deployment and database evidence converged.",
            [
                new IncidentEvidence(
                    "deploy-1",
                    EvidenceType.Deployment,
                    "kubernetes",
                    DateTimeOffset.Parse("2026-09-19T15:55:00Z"),
                    "payment-service",
                    "Deployment rolled out",
                    "image payment-service:v2",
                    new Dictionary<string, string>
                    {
                        ["deployment.version"] = "v2"
                    }),
                new IncidentEvidence(
                    "log-1",
                    EvidenceType.Log,
                    "loki",
                    DateTimeOffset.Parse("2026-09-19T16:02:00Z"),
                    "payment-service",
                    "Database timeout",
                    "timeout waiting for pooled connection",
                    new Dictionary<string, string>
                    {
                        ["level"] = "error"
                    })
            ],
            [
                new RootCauseHypothesis(
                    1,
                    "Connection pool pressure",
                    "Deployment and timeout evidence align.",
                    0.9,
                    ["deploy-1", "log-1"])
            ],
            [
                "Compare pool saturation before and after deployment."
            ])
        {
            ReasoningTelemetry = new ReasoningTelemetry(
                "llm",
                "openai",
                "test-model",
                100,
                50,
                120,
                0.001m),
            SourceExecutions =
            [
                new SourceExecutionTelemetry(
                    "kubernetes",
                    "success",
                    25,
                    1),
                new SourceExecutionTelemetry(
                    "loki",
                    "success",
                    30,
                    1)
            ]
        };

        await store.SaveAsync(request, investigation);

        var loaded = await store.GetAsync(
            investigation.InvestigationId);

        Assert.NotNull(loaded);
        Assert.Equal(request, loaded!.Request);
        Assert.Equal(investigation.InvestigationId,
            loaded.Investigation.InvestigationId);
        Assert.Equal(2, loaded.Investigation.Evidence.Count);
        Assert.Equal("v2",
            loaded.Investigation.Evidence[0].Attributes["deployment.version"]);
        Assert.Single(loaded.Investigation.Hypotheses);
        Assert.Equal(
            new[] { "deploy-1", "log-1" },
            loaded.Investigation.Hypotheses[0].EvidenceIds);
        Assert.Equal("llm",
            loaded.Investigation.ReasoningTelemetry?.Mode);
        Assert.Equal(2, loaded.Investigation.SourceExecutions.Count);

        var history = await store.SearchAsync(
            new InvestigationHistoryQuery(
                ServiceName: "payment-service",
                FromUtc: DateTimeOffset.Parse("2026-09-19T00:00:00Z"),
                ToUtc: DateTimeOffset.Parse("2026-09-20T00:00:00Z"),
                Page: 1,
                PageSize: 10));

        Assert.Equal(1, history.TotalCount);
        var item = Assert.Single(history.Items);
        Assert.Equal(investigation.InvestigationId, item.InvestigationId);
        Assert.Equal(0.9, item.PrimaryConfidence);
        Assert.Equal(2, item.EvidenceCount);

        var filteredOut = await store.SearchAsync(
            new InvestigationHistoryQuery(
                ServiceName: "another-service"));

        Assert.Equal(0, filteredOut.TotalCount);
        Assert.Empty(filteredOut.Items);
    }
}
