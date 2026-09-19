namespace IncidentAgent.Core;

public interface IIncidentEvidenceSource
{
    string Name { get; }

    Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default);
}

public interface IIncidentInvestigator
{
    Task<IncidentInvestigation> InvestigateAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default);
}
