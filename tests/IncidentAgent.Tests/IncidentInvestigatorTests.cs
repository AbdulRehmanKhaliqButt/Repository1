using IncidentAgent.Core;
using IncidentAgent.Infrastructure;

namespace IncidentAgent.Tests;

public sealed class IncidentInvestigatorTests
{
    private static IncidentInvestigator CreateSubject() =>
        new(
        [
            new DemoDeploymentEvidenceSource(),
            new DemoCommitEvidenceSource(),
            new DemoMetricEvidenceSource(),
            new DemoLogEvidenceSource(),
            new DemoTraceEvidenceSource()
        ]);

    [Fact]
    public async Task InvestigateAsync_CorrelatesDemoEvidence_AndRanksDatabaseHypothesisFirst()
    {
        var subject = CreateSubject();
        var request = new IncidentRequest(
            "Checkout failures",
            "Checkout requests are timing out.",
            "payment-service",
            DateTimeOffset.UtcNow);

        var result = await subject.InvestigateAsync(request);

        Assert.Equal(5, result.Evidence.Count);
        Assert.Equal(2, result.Hypotheses.Count);
        Assert.Contains("Database connection pool", result.Hypotheses[0].Title);
        Assert.True(result.Hypotheses[0].Confidence >= 0.90);
        Assert.Equal(5, result.Hypotheses[0].EvidenceIds.Count);
        Assert.NotEmpty(result.RecommendedActions);
    }

    [Fact]
    public async Task InvestigateAsync_PreservesRequestedServiceAcrossEvidence()
    {
        var subject = CreateSubject();
        var request = new IncidentRequest(
            "API slowdown",
            "Requests are slow.",
            "billing-service",
            DateTimeOffset.UtcNow);

        var result = await subject.InvestigateAsync(request);

        Assert.All(result.Evidence, evidence => Assert.Equal("billing-service", evidence.Service));
    }
}
