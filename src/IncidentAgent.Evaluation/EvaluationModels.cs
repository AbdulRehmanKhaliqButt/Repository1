using IncidentAgent.Core;

namespace IncidentAgent.Evaluation;

public sealed record EvaluationDataset(
    IReadOnlyList<EvaluationCase> Cases);

public sealed record EvaluationCase(
    string Id,
    IncidentRequest Request,
    IReadOnlyList<IncidentEvidence> Evidence,
    EvaluationExpectation Expected);

public sealed record EvaluationExpectation(
    string PrimaryTitleContains,
    IReadOnlyList<string> ExpectedEvidenceIds);

public sealed record EvaluationCaseResult(
    string Id,
    bool Top1Match,
    bool Top3Match,
    double CitationPrecision,
    double CitationRecall,
    double UnsupportedClaimRate,
    long LatencyMs,
    decimal EstimatedCostUsd,
    string PrimaryHypothesis,
    IReadOnlyList<string> PrimaryEvidenceIds);

public sealed record EvaluationSummary(
    int CaseCount,
    double Top1Accuracy,
    double Top3Accuracy,
    double CitationPrecision,
    double CitationRecall,
    double UnsupportedClaimRate,
    double AverageLatencyMs,
    decimal TotalEstimatedCostUsd,
    IReadOnlyList<EvaluationCaseResult> Cases,
    EvaluationThresholds Thresholds,
    bool Passed);

public sealed record EvaluationThresholds(
    double MinTop1Accuracy = 0.80,
    double MinTop3Accuracy = 0.90,
    double MinCitationPrecision = 0.95,
    double MinCitationRecall = 0.80,
    double MaxUnsupportedClaimRate = 0.05);
