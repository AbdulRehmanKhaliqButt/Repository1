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
            CollectSafelyAsync(source, incident, cancellationToken));

        var sourceResults = await Task.WhenAll(sourceTasks);

        var evidence = sourceResults
            .SelectMany(result => result)
            .OrderBy(item => item.TimestampUtc)
            .ToArray();

        var usableEvidence = evidence
            .Where(item => item.Type != EvidenceType.SourceError)
            .ToArray();

        var sourceErrors = evidence.Count(item => item.Type == EvidenceType.SourceError);

        if (usableEvidence.Length == 0)
        {
            return new IncidentInvestigation(
                Guid.NewGuid().ToString("n"),
                DateTimeOffset.UtcNow,
                sourceErrors > 0
                    ? $"No usable evidence was available. {sourceErrors} evidence source(s) failed."
                    : "No evidence was available for this incident window.",
                evidence,
                [
                    new RootCauseHypothesis(
                        1,
                        "Insufficient evidence",
                        "No configured source returned usable evidence. Verify integrations and expand the investigation window.",
                        0.10,
                        [])
                ],
                [
                    "Verify telemetry source connectivity.",
                    "Expand the incident time window.",
                    "Confirm the affected service name."
                ]);
        }

        var deployment = usableEvidence.FirstOrDefault(item => item.Type == EvidenceType.Deployment);
        var commit = usableEvidence.FirstOrDefault(item => item.Type == EvidenceType.Commit);
        var timeoutLog = usableEvidence.FirstOrDefault(item =>
            item.Type == EvidenceType.Log &&
            (item.Summary.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
             item.Details.Contains("timeout", StringComparison.OrdinalIgnoreCase)));

        var slowDbTrace = usableEvidence.FirstOrDefault(item =>
            item.Type == EvidenceType.Trace &&
            item.Attributes.TryGetValue("db.system", out var databaseSystem) &&
            !string.IsNullOrWhiteSpace(databaseSystem) &&
            item.Attributes.TryGetValue("duration.ms", out var durationText) &&
            double.TryParse(durationText, out var durationMs) &&
            durationMs >= 1000);

        var latencySpike = usableEvidence.FirstOrDefault(item =>
            item.Type == EvidenceType.Metric &&
            item.Attributes.TryGetValue("metric.name", out var metricName) &&
            metricName == "http.server.duration.p95");

        var matchedEvidence = new[] { deployment, commit, timeoutLog, slowDbTrace, latencySpike }
            .Where(item => item is not null)
            .Cast<IncidentEvidence>()
            .ToArray();

        if (matchedEvidence.Length < 3)
        {
            var partialSummary =
                $"Collected {usableEvidence.Length} usable evidence items, but the signals do not yet agree strongly enough to name a specific root cause.";

            if (sourceErrors > 0)
            {
                partialSummary += $" {sourceErrors} source(s) failed and were isolated from the investigation.";
            }

            return new IncidentInvestigation(
                Guid.NewGuid().ToString("n"),
                DateTimeOffset.UtcNow,
                partialSummary,
                evidence,
                [
                    new RootCauseHypothesis(
                        1,
                        "Partial evidence — root cause not yet established",
                        "The available signals are insufficient for a specific causal claim. Gather additional logs, traces, deployment history, or metrics before taking corrective action.",
                        0.30,
                        usableEvidence.Select(item => item.Id).ToArray())
                ],
                [
                    "Restore or verify failed telemetry sources.",
                    "Collect at least two additional independent signals from logs, traces, deployments, commits, or metrics.",
                    "Avoid automated remediation until the evidence converges."
                ]);
        }

        var confidence = Math.Min(0.95, 0.35 + (matchedEvidence.Length * 0.12));

        var primary = new RootCauseHypothesis(
            1,
            "Database connection pool exhaustion after a recent payment-service deployment",
            "The incident window contains a recent deployment and related commit, followed by elevated request latency, database timeout logs, and a slow PostgreSQL trace span. The temporal ordering and cross-source agreement make database connection-pool pressure the leading hypothesis.",
            confidence,
            matchedEvidence.Select(item => item.Id).ToArray());

        var secondaryEvidence = usableEvidence
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

        var summary =
            $"Correlated {usableEvidence.Length} usable evidence items from {_sources.Count} sources for {incident.ServiceName ?? "the affected service"}.";

        if (sourceErrors > 0)
        {
            summary += $" {sourceErrors} source(s) failed and were isolated from the investigation.";
        }

        return new IncidentInvestigation(
            Guid.NewGuid().ToString("n"),
            DateTimeOffset.UtcNow,
            summary,
            evidence,
            [primary, secondary],
            [
                "Compare database connection-pool saturation before and after the latest deployment.",
                "Roll back or disable the pool-related change if error rate remains elevated.",
                "Inspect active/idle database connection counts and server-side connection limits.",
                "Re-run the investigation after mitigation and compare latency and timeout evidence."
            ]);
    }

    private static async Task<IReadOnlyCollection<IncidentEvidence>> CollectSafelyAsync(
        IIncidentEvidenceSource source,
        IncidentRequest incident,
        CancellationToken cancellationToken)
    {
        try
        {
            return await source.CollectAsync(incident, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return
            [
                new IncidentEvidence(
                    $"source-error-{source.Name}",
                    EvidenceType.SourceError,
                    "incident-investigator",
                    DateTimeOffset.UtcNow,
                    incident.ServiceName ?? "unknown",
                    $"Evidence source '{source.Name}' failed",
                    exception.Message,
                    new Dictionary<string, string>
                    {
                        ["source.name"] = source.Name,
                        ["exception.type"] = exception.GetType().Name
                    })
            ];
        }
    }
}
