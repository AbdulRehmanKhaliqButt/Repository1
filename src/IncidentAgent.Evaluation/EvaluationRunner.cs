using System.Diagnostics;
using IncidentAgent.Core;

namespace IncidentAgent.Evaluation;

public sealed class EvaluationRunner(
    IIncidentReasoner reasoner,
    EvaluationThresholds thresholds)
{
    public async Task<EvaluationSummary> RunAsync(
        EvaluationDataset dataset,
        CancellationToken cancellationToken = default)
    {
        if (dataset.Cases.Count == 0)
        {
            throw new InvalidOperationException("Evaluation dataset contains no cases.");
        }

        var results = new List<EvaluationCaseResult>(dataset.Cases.Count);

        foreach (var item in dataset.Cases)
        {
            var stopwatch = Stopwatch.StartNew();
            var reasoning = await reasoner.ReasonAsync(
                item.Request,
                item.Evidence,
                cancellationToken);
            stopwatch.Stop();

            var ranked = reasoning.Hypotheses
                .OrderBy(hypothesis => hypothesis.Rank)
                .ToArray();

            var primary = ranked.FirstOrDefault();
            var top1Match = primary is not null &&
                primary.Title.Contains(
                    item.Expected.PrimaryTitleContains,
                    StringComparison.OrdinalIgnoreCase);

            var top3Match = ranked.Take(3).Any(hypothesis =>
                hypothesis.Title.Contains(
                    item.Expected.PrimaryTitleContains,
                    StringComparison.OrdinalIgnoreCase));

            var availableEvidence = item.Evidence
                .Select(evidence => evidence.Id)
                .ToHashSet(StringComparer.Ordinal);

            var allCitations = ranked
                .SelectMany(hypothesis => hypothesis.EvidenceIds)
                .ToArray();

            var supportedCitationCount = allCitations.Count(availableEvidence.Contains);
            var unsupportedCitationCount = allCitations.Length - supportedCitationCount;

            var caseCitationPrecision = allCitations.Length == 0
                ? 1d
                : (double)supportedCitationCount / allCitations.Length;

            var caseUnsupportedClaimRate = allCitations.Length == 0
                ? 0d
                : (double)unsupportedCitationCount / allCitations.Length;

            var expectedIds = item.Expected.ExpectedEvidenceIds
                .ToHashSet(StringComparer.Ordinal);

            var primaryIds = primary?.EvidenceIds
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);

            var recalled = expectedIds.Count == 0
                ? expectedIds.Count
                : expectedIds.Count(primaryIds.Contains);

            var caseCitationRecall = expectedIds.Count == 0
                ? 1d
                : (double)recalled / expectedIds.Count;

            results.Add(new EvaluationCaseResult(
                item.Id,
                top1Match,
                top3Match,
                caseCitationPrecision,
                caseCitationRecall,
                caseUnsupportedClaimRate,
                stopwatch.ElapsedMilliseconds,
                reasoning.Telemetry.EstimatedCostUsd,
                primary?.Title ?? "<none>",
                primary?.EvidenceIds ?? Array.Empty<string>()));
        }

        var top1Accuracy = results.Average(result => result.Top1Match ? 1d : 0d);
        var top3Accuracy = results.Average(result => result.Top3Match ? 1d : 0d);
        var citationPrecision = results.Average(result => result.CitationPrecision);
        var citationRecall = results.Average(result => result.CitationRecall);
        var unsupportedClaimRate = results.Average(result => result.UnsupportedClaimRate);
        var averageLatencyMs = results.Average(result => (double)result.LatencyMs);
        var totalEstimatedCostUsd = results.Sum(result => result.EstimatedCostUsd);

        var passed =
            top1Accuracy >= thresholds.MinTop1Accuracy &&
            top3Accuracy >= thresholds.MinTop3Accuracy &&
            citationPrecision >= thresholds.MinCitationPrecision &&
            citationRecall >= thresholds.MinCitationRecall &&
            unsupportedClaimRate <= thresholds.MaxUnsupportedClaimRate;

        return new EvaluationSummary(
            results.Count,
            top1Accuracy,
            top3Accuracy,
            citationPrecision,
            citationRecall,
            unsupportedClaimRate,
            averageLatencyMs,
            totalEstimatedCostUsd,
            results,
            thresholds,
            passed);
    }
}
