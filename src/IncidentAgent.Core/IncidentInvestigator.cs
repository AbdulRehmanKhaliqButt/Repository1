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
            .SelectMany(result => result)
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
            ReasoningTelemetry = reasoning.Telemetry
        };
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
