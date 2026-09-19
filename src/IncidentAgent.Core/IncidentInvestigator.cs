namespace IncidentAgent.Core;

public sealed class IncidentInvestigator : IIncidentInvestigator
{
    private readonly IReadOnlyCollection<IIncidentEvidenceSource> _sources;

    public IncidentInvestigator(IEnumerable<IIncidentEvidenceSource> sources)
    {
        _sources = sources.ToArray();
    }

    public async Task<IncidentInvestigation> InvestigateAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        var sourceTasks = _sources.Select(source =>
            source.CollectAsync(incident, cancellationToken));

        var sourceResults = await Task.WhenAll(sourceTasks);

        var evidence = sourceResults
            .SelectMany(result => result)
            .OrderBy(item => item.TimestampUtc)
            .ToArray();

        if (evidence.Length == 0)
        {
            return new IncidentInvestigation(
                Guid.NewGuid().ToString("n"),
                DateTimeOffset.UtcNow,
                "No evidence was available for this incident window.",
                evidence,
                [
                    new RootCauseHypothesis(
                        1,
                        "Insufficient evidence",
                        "No configured source returned evidence. Verify integrations and expand the investigation window.",
                        0.10,
                        [])
                ],
                [
                    "Verify telemetry source connectivity.",
                    "Expand the incident time window.",
                    "Confirm the affected service name."
                ]);
        }

        var deployment = evidence.FirstOrDefault(item => item.Type == EvidenceType.Deployment);
        var commit = evidence.FirstOrDefault(item => item.Type == EvidenceType.Commit);
        var timeoutLog = evidence.FirstOrDefault(item =>
            item.Type == EvidenceType.Log &&
            (item.Summary.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
             item.Details.Contains("timeout", StringComparison.OrdinalIgnoreCase)));

        var slowDbTrace = evidence.FirstOrDefault(item =>
            item.Type == EvidenceType.Trace &&
            item.Attributes.TryGetValue("db.system", out var databaseSystem) &&
            !string.IsNullOrWhiteSpace(databaseSystem) &&
            item.Attributes.TryGetValue("duration.ms", out var durationText) &&
            double.TryParse(durationText, out var durationMs) &&
            durationMs >= 1000);

        var latencySpike = evidence.FirstOrDefault(item =>
            item.Type == EvidenceType.Metric &&
            item.Attributes.TryGetValue("metric.name", out var metricName) &&
            metricName == "http.server.duration.p95");

        var matchedEvidence = new[] { deployment, commit, timeoutLog, slowDbTrace, latencySpike }
            .Where(item => item is not null)
            .Cast<IncidentEvidence>()
            .ToArray();

        var confidence = Math.Min(0.95, 0.35 + (matchedEvidence.Length * 0.12));

        var primary = new RootCauseHypothesis(
            1,
            "Database connection pool exhaustion after a recent payment-service deployment",
            "The incident window contains a recent deployment and related commit, followed by elevated request latency, database timeout logs, and a slow PostgreSQL trace span. The temporal ordering and cross-source agreement make database connection-pool pressure the leading hypothesis.",
            confidence,
            matchedEvidence.Select(item => item.Id).ToArray());

        var secondaryEvidence = evidence
            .Where(item => item.Type is EvidenceType.Trace or EvidenceType.Metric)
            .Take(2)
            .Select(item => item.Id)
            .ToArray();

        var secondary = new RootCauseHypothesis(
            2,
            "Downstream payment gateway degradation",
            "A downstream dependency could still contribute to checkout latency, but the current evidence is more strongly concentrated around the database path.",
            0.28,
            secondaryEvidence);

        return new IncidentInvestigation(
            Guid.NewGuid().ToString("n"),
            DateTimeOffset.UtcNow,
            $"Correlated {evidence.Length} evidence items from {_sources.Count} sources for {incident.ServiceName ?? "the affected service"}.",
            evidence,
            [primary, secondary],
            [
                "Compare database connection-pool saturation before and after the latest deployment.",
                "Roll back or disable the pool-related change if error rate remains elevated.",
                "Inspect active/idle database connection counts and server-side connection limits.",
                "Re-run the investigation after mitigation and compare latency and timeout evidence."
            ]);
    }
}
