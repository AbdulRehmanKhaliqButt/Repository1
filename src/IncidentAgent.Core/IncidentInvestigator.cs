using System.Diagnostics;

namespace IncidentAgent.Core;

public sealed class IncidentInvestigator : IIncidentInvestigator
{
    private readonly IReadOnlyCollection<IIncidentEvidenceSource> _sources;
    private readonly IIncidentReasoner _reasoner;

    public IncidentInvestigator(IEnumerable<IIncidentEvidenceSource> sources)
        : this(sources, new DeterministicIncidentReasoner())
    {
    }

    public IncidentInvestigator(
        IEnumerable<IIncidentEvidenceSource> sources,
        IIncidentReasoner reasoner)
    {
        _sources = sources.ToArray();
        _reasoner = reasoner;
    }

    public async Task<IncidentInvestigation> InvestigateAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        var sourceTasks = _sources.Select(source =>
            CollectSafelyAsync(source, incident, cancellationToken));

        var sourceResults = await Task.WhenAll(sourceTasks);

        var evidence = sourceResults
            .SelectMany(result => result.Evidence)
            .OrderBy(item => item.TimestampUtc)
            .ToArray();

        var reasoning = await _reasoner.ReasonAsync(
            incident,
            evidence,
            cancellationToken);

        return new IncidentInvestigation(
            Guid.NewGuid().ToString("n"),
            DateTimeOffset.UtcNow,
            reasoning.Summary,
            evidence,
            reasoning.Hypotheses,
            reasoning.RecommendedActions)
        {
            ReasoningTelemetry = reasoning.Telemetry,
            SourceExecutions = sourceResults
                .Select(result => result.Telemetry)
                .OrderBy(item => item.Source, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static async Task<SourceCollectionResult> CollectSafelyAsync(
        IIncidentEvidenceSource source,
        IncidentRequest incident,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var evidence = await source.CollectAsync(incident, cancellationToken);
            stopwatch.Stop();

            return new SourceCollectionResult(
                evidence,
                new SourceExecutionTelemetry(
                    source.Name,
                    "success",
                    stopwatch.ElapsedMilliseconds,
                    evidence.Count));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            var evidence = new[]
            {
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
            };

            return new SourceCollectionResult(
                evidence,
                new SourceExecutionTelemetry(
                    source.Name,
                    "error",
                    stopwatch.ElapsedMilliseconds,
                    evidence.Length,
                    exception.GetType().Name));
        }
    }

    private sealed record SourceCollectionResult(
        IReadOnlyCollection<IncidentEvidence> Evidence,
        SourceExecutionTelemetry Telemetry);
}
