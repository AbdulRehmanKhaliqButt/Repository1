using System.Text.Json;
using IncidentAgent.Core;
using Microsoft.EntityFrameworkCore;

namespace IncidentAgent.Persistence;

public sealed class PostgresInvestigationStore(
    InvestigationDbContext dbContext) : IInvestigationStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(
        IncidentRequest request,
        IncidentInvestigation investigation,
        CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(request, investigation);

        dbContext.Investigations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredInvestigation?> GetAsync(
        string investigationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Investigations
            .AsNoTracking()
            .Include(item => item.Evidence)
            .Include(item => item.Hypotheses)
            .Include(item => item.Actions)
            .Include(item => item.SourceExecutions)
            .SingleOrDefaultAsync(
                item => item.Id == investigationId,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<InvestigationHistoryPage> SearchAsync(
        InvestigationHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var databaseQuery = dbContext.Investigations
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ServiceName))
        {
            databaseQuery = databaseQuery.Where(item =>
                item.ServiceName == query.ServiceName);
        }

        if (query.FromUtc.HasValue)
        {
            databaseQuery = databaseQuery.Where(item =>
                item.GeneratedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            databaseQuery = databaseQuery.Where(item =>
                item.GeneratedAtUtc <= query.ToUtc.Value);
        }

        var totalCount = await databaseQuery.CountAsync(cancellationToken);

        var items = await databaseQuery
            .OrderByDescending(item => item.GeneratedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new InvestigationHistoryItem(
                item.Id,
                item.GeneratedAtUtc,
                item.Title,
                item.ServiceName,
                item.StartedAtUtc,
                item.Summary,
                item.ReasoningMode ?? "unknown",
                item.Hypotheses
                    .OrderBy(hypothesis => hypothesis.Rank)
                    .Select(hypothesis => (double?)hypothesis.Confidence)
                    .FirstOrDefault(),
                item.Evidence.Count))
            .ToListAsync(cancellationToken);

        return new InvestigationHistoryPage(
            items,
            page,
            pageSize,
            totalCount);
    }

    private static InvestigationEntity ToEntity(
        IncidentRequest request,
        IncidentInvestigation investigation)
    {
        var telemetry = investigation.ReasoningTelemetry;

        var entity = new InvestigationEntity
        {
            Id = investigation.InvestigationId,
            GeneratedAtUtc = investigation.GeneratedAtUtc,
            Title = request.Title,
            Description = request.Description,
            ServiceName = request.ServiceName,
            StartedAtUtc = request.StartedAtUtc,
            Summary = investigation.Summary,
            ReasoningMode = telemetry?.Mode,
            ReasoningProvider = telemetry?.Provider,
            ReasoningModel = telemetry?.Model,
            ReasoningInputTokens = telemetry?.InputTokens ?? 0,
            ReasoningOutputTokens = telemetry?.OutputTokens ?? 0,
            ReasoningLatencyMs = telemetry?.LatencyMs ?? 0,
            ReasoningEstimatedCostUsd = telemetry?.EstimatedCostUsd ?? 0m,
            ReasoningFallbackReason = telemetry?.FallbackReason
        };

        entity.Evidence = investigation.Evidence.Select(item =>
            new EvidenceEntity
            {
                InvestigationId = investigation.InvestigationId,
                EvidenceId = item.Id,
                Type = item.Type.ToString(),
                Source = item.Source,
                TimestampUtc = item.TimestampUtc,
                Service = item.Service,
                Summary = item.Summary,
                Details = item.Details,
                AttributesJson = JsonSerializer.Serialize(
                    item.Attributes,
                    JsonOptions)
            }).ToList();

        entity.Hypotheses = investigation.Hypotheses.Select(item =>
            new HypothesisEntity
            {
                InvestigationId = investigation.InvestigationId,
                Rank = item.Rank,
                Title = item.Title,
                Explanation = item.Explanation,
                Confidence = item.Confidence,
                EvidenceIdsJson = JsonSerializer.Serialize(
                    item.EvidenceIds,
                    JsonOptions)
            }).ToList();

        entity.Actions = investigation.RecommendedActions
            .Select((text, index) =>
                new RecommendedActionEntity
                {
                    InvestigationId = investigation.InvestigationId,
                    SortOrder = index,
                    Text = text
                })
            .ToList();

        entity.SourceExecutions = investigation.SourceExecutions.Select(item =>
            new SourceExecutionEntity
            {
                InvestigationId = investigation.InvestigationId,
                Source = item.Source,
                Status = item.Status,
                DurationMs = item.DurationMs,
                EvidenceCount = item.EvidenceCount,
                ErrorType = item.ErrorType
            }).ToList();

        return entity;
    }

    private static StoredInvestigation ToDomain(InvestigationEntity entity)
    {
        var telemetry = string.IsNullOrWhiteSpace(entity.ReasoningMode)
            ? null
            : new ReasoningTelemetry(
                entity.ReasoningMode!,
                entity.ReasoningProvider ?? string.Empty,
                entity.ReasoningModel ?? string.Empty,
                entity.ReasoningInputTokens,
                entity.ReasoningOutputTokens,
                entity.ReasoningLatencyMs,
                entity.ReasoningEstimatedCostUsd,
                entity.ReasoningFallbackReason);

        var investigation = new IncidentInvestigation(
            entity.Id,
            entity.GeneratedAtUtc,
            entity.Summary,
            entity.Evidence
                .OrderBy(item => item.TimestampUtc)
                .Select(item => new IncidentEvidence(
                    item.EvidenceId,
                    Enum.TryParse<EvidenceType>(
                        item.Type,
                        ignoreCase: true,
                        out var type)
                        ? type
                        : EvidenceType.SourceError,
                    item.Source,
                    item.TimestampUtc,
                    item.Service,
                    item.Summary,
                    item.Details,
                    DeserializeDictionary(item.AttributesJson)))
                .ToArray(),
            entity.Hypotheses
                .OrderBy(item => item.Rank)
                .Select(item => new RootCauseHypothesis(
                    item.Rank,
                    item.Title,
                    item.Explanation,
                    item.Confidence,
                    DeserializeList(item.EvidenceIdsJson)))
                .ToArray(),
            entity.Actions
                .OrderBy(item => item.SortOrder)
                .Select(item => item.Text)
                .ToArray())
        {
            ReasoningTelemetry = telemetry,
            SourceExecutions = entity.SourceExecutions
                .OrderBy(item => item.Source)
                .Select(item => new SourceExecutionTelemetry(
                    item.Source,
                    item.Status,
                    item.DurationMs,
                    item.EvidenceCount,
                    item.ErrorType))
                .ToArray()
        };

        return new StoredInvestigation(
            new IncidentRequest(
                entity.Title,
                entity.Description,
                entity.ServiceName,
                entity.StartedAtUtc),
            investigation);
    }

    private static IReadOnlyDictionary<string, string> DeserializeDictionary(
        string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            json,
            JsonOptions)
        ?? new Dictionary<string, string>();

    private static IReadOnlyList<string> DeserializeList(string json) =>
        JsonSerializer.Deserialize<List<string>>(json, JsonOptions)
        ?? [];
}
