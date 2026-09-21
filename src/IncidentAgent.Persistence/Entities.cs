namespace IncidentAgent.Persistence;

public sealed class InvestigationEntity
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public string Summary { get; set; } = string.Empty;

    public string? ReasoningMode { get; set; }
    public string? ReasoningProvider { get; set; }
    public string? ReasoningModel { get; set; }
    public int ReasoningInputTokens { get; set; }
    public int ReasoningOutputTokens { get; set; }
    public long ReasoningLatencyMs { get; set; }
    public decimal ReasoningEstimatedCostUsd { get; set; }
    public string? ReasoningFallbackReason { get; set; }

    public List<EvidenceEntity> Evidence { get; set; } = [];
    public List<HypothesisEntity> Hypotheses { get; set; } = [];
    public List<RecommendedActionEntity> Actions { get; set; } = [];
    public List<SourceExecutionEntity> SourceExecutions { get; set; } = [];
}

public sealed class EvidenceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvestigationId { get; set; } = string.Empty;
    public InvestigationEntity Investigation { get; set; } = null!;

    public string EvidenceId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string AttributesJson { get; set; } = "{}";
}

public sealed class HypothesisEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvestigationId { get; set; } = string.Empty;
    public InvestigationEntity Investigation { get; set; } = null!;

    public int Rank { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string EvidenceIdsJson { get; set; } = "[]";
}

public sealed class RecommendedActionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvestigationId { get; set; } = string.Empty;
    public InvestigationEntity Investigation { get; set; } = null!;

    public int SortOrder { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class SourceExecutionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvestigationId { get; set; } = string.Empty;
    public InvestigationEntity Investigation { get; set; } = null!;

    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public int EvidenceCount { get; set; }
    public string? ErrorType { get; set; }
}
