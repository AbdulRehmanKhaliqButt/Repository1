namespace IncidentAgent.Core;

public enum EvidenceType
{
    Log,
    Trace,
    Metric,
    Deployment,
    Commit,
    SourceError
}

public sealed record IncidentRequest(
    string Title,
    string Description,
    string? ServiceName,
    DateTimeOffset StartedAtUtc);

public sealed record IncidentEvidence(
    string Id,
    EvidenceType Type,
    string Source,
    DateTimeOffset TimestampUtc,
    string Service,
    string Summary,
    string Details,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record RootCauseHypothesis(
    int Rank,
    string Title,
    string Explanation,
    double Confidence,
    IReadOnlyList<string> EvidenceIds);

public sealed record ReasoningTelemetry(
    string Mode,
    string Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    long LatencyMs,
    decimal EstimatedCostUsd,
    string? FallbackReason = null);

public sealed record IncidentReasoning(
    string Summary,
    IReadOnlyList<RootCauseHypothesis> Hypotheses,
    IReadOnlyList<string> RecommendedActions,
    ReasoningTelemetry Telemetry);

public sealed record LlmGenerationResult(
    string Content,
    string Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    long LatencyMs,
    decimal EstimatedCostUsd);

public sealed record IncidentInvestigation(
    string InvestigationId,
    DateTimeOffset GeneratedAtUtc,
    string Summary,
    IReadOnlyList<IncidentEvidence> Evidence,
    IReadOnlyList<RootCauseHypothesis> Hypotheses,
    IReadOnlyList<string> RecommendedActions)
{
    public ReasoningTelemetry? ReasoningTelemetry { get; init; }
}
