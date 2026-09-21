using IncidentAgent.Core;

namespace IncidentAgent.Persistence;

public sealed class NullInvestigationStore : IInvestigationStore
{
    public Task SaveAsync(
        IncidentRequest request,
        IncidentInvestigation investigation,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<StoredInvestigation?> GetAsync(
        string investigationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<StoredInvestigation?>(null);

    public Task<InvestigationHistoryPage> SearchAsync(
        InvestigationHistoryQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new InvestigationHistoryPage(
            [],
            Math.Max(1, query.Page),
            Math.Clamp(query.PageSize, 1, 100),
            0));
}
