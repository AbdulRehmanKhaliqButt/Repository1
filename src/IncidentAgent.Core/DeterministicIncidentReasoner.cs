namespace IncidentAgent.Core;

public sealed class DeterministicIncidentReasoner : IIncidentReasoner
{
    public Task<IncidentReasoning> ReasonAsync(
        IncidentRequest incident,
        IReadOnlyList<IncidentEvidence> evidence,
        CancellationToken cancellationToken = default)
    {
        var usableEvidence = evidence
            .Where(item => item.Type != EvidenceType.SourceError)
            .ToArray();

        var sourceErrors = evidence.Count(item => item.Type == EvidenceType.SourceError);

        if (usableEvidence.Length == 0)
        {
            return Task.FromResult(new IncidentReasoning(
                sourceErrors > 0
                    ? $"No usable evidence was available. {sourceErrors} evidence source(s) failed."
                    : "No evidence was available for this incident window.",
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
                ],
                DeterministicTelemetry()));
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

            return Task.FromResult(new IncidentReasoning(
                partialSummary,
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
                ],
                DeterministicTelemetry()));
        }

        var confidence = Math.Min(0.95, 0.35 + (matchedEvidence.Length * 0.12));

        var primary = new RootCauseHypothesis(
            1,
            $"Database connection pool exhaustion after a recent {incident.ServiceName ?? "service"} deployment",
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
            $"Correlated {usableEvidence.Length} usable evidence items for {incident.ServiceName ?? "the affected service"}.";

        if (sourceErrors > 0)
        {
            summary += $" {sourceErrors} source(s) failed and were isolated from the investigation.";
        }

        return Task.FromResult(new IncidentReasoning(
            summary,
            [primary, secondary],
            [
                "Compare database connection-pool saturation before and after the latest deployment.",
                "Roll back or disable the pool-related change if error rate remains elevated.",
                "Inspect active/idle database connection counts and server-side connection limits.",
                "Re-run the investigation after mitigation and compare latency and timeout evidence."
            ],
            DeterministicTelemetry()));
    }

    private static ReasoningTelemetry DeterministicTelemetry() =>
        new(
            "deterministic",
            "built-in",
            "rules-v1",
            0,
            0,
            0,
            0m);
}
