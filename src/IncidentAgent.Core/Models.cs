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

public sealed record SourceExecutionTelemetry(
    string Source,
    string Status,
    long DurationMs,
    int EvidenceCount,
    string? ErrorType = null);

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

    public IReadOnlyList<SourceExecutionTelemetry> SourceExecutions { get; init; } =
        Array.Empty<SourceExecutionTelemetry>();
}

public sealed record StoredInvestigation(
    IncidentRequest Request,
    IncidentInvestigation Investigation);

public sealed record InvestigationHistoryQuery(
    string? ServiceName = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Page = 1,
    int PageSize = 20);

public sealed record InvestigationHistoryItem(
    string InvestigationId,
    DateTimeOffset GeneratedAtUtc,
    string Title,
    string? ServiceName,
    DateTimeOffset StartedAtUtc,
    string Summary,
    string ReasoningMode,
    double? PrimaryConfidence,
    int EvidenceCount);

public sealed record InvestigationHistoryPage(
    IReadOnlyList<InvestigationHistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
