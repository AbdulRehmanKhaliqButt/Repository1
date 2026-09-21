namespace IncidentAgent.Core;

public interface IIncidentEvidenceSource
{
    string Name { get; }

    Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default);
}

public interface IIncidentReasoner
{
    Task<IncidentReasoning> ReasonAsync(
        IncidentRequest incident,
        IReadOnlyList<IncidentEvidence> evidence,
        CancellationToken cancellationToken = default);
}

public interface IIncidentLlmProvider
{
    string Name { get; }

    Task<LlmGenerationResult> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}

public interface IIncidentInvestigator
{
    Task<IncidentInvestigation> InvestigateAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default);
}
