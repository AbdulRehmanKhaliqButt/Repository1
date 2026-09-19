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

public sealed record IncidentInvestigation(
    string InvestigationId,
    DateTimeOffset GeneratedAtUtc,
    string Summary,
    IReadOnlyList<IncidentEvidence> Evidence,
    IReadOnlyList<RootCauseHypothesis> Hypotheses,
    IReadOnlyList<string> RecommendedActions);
