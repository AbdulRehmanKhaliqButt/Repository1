using System.Text.Json.Serialization;
using IncidentAgent.Core;
using IncidentAgent.Infrastructure;
using IncidentAgent.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddIncidentInvestigation(builder.Configuration);
builder.Services.AddInvestigationPersistence(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeInvestigationPersistenceAsync(
    builder.Configuration);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "incident-investigation-agent",
    timestampUtc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/demo/scenario", () => Results.Ok(new IncidentRequest(
    "Checkout failures",
    "Customers report checkout requests timing out shortly after a production deployment.",
    "payment-service",
    DateTimeOffset.UtcNow.AddMinutes(-10))));

app.MapPost("/api/incidents/investigate", async (
    IncidentRequest request,
    IIncidentInvestigator investigator,
    IInvestigationStore store,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "title is required" });
    }

    var normalized = request.StartedAtUtc == default
        ? request with { StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10) }
        : request;

    var investigation = await investigator.InvestigateAsync(
        normalized,
        cancellationToken);

    await store.SaveAsync(
        normalized,
        investigation,
        cancellationToken);

    return Results.Ok(investigation);
});

app.MapGet("/api/investigations/{investigationId}", async (
    string investigationId,
    IInvestigationStore store,
    CancellationToken cancellationToken) =>
{
    var stored = await store.GetAsync(
        investigationId,
        cancellationToken);

    return stored is null
        ? Results.NotFound(new { error = "investigation not found" })
        : Results.Ok(stored);
});

app.MapGet("/api/investigations", async (
    string? serviceName,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    int? page,
    int? pageSize,
    IInvestigationStore store,
    CancellationToken cancellationToken) =>
{
    var result = await store.SearchAsync(
        new InvestigationHistoryQuery(
            serviceName,
            fromUtc,
            toUtc,
            page ?? 1,
            pageSize ?? 20),
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/investigations/compare", async (
    string leftId,
    string rightId,
    IInvestigationStore store,
    CancellationToken cancellationToken) =>
{
    var leftTask = store.GetAsync(leftId, cancellationToken);
    var rightTask = store.GetAsync(rightId, cancellationToken);

    await Task.WhenAll(leftTask, rightTask);

    var left = await leftTask;
    var right = await rightTask;

    if (left is null || right is null)
    {
        return Results.NotFound(new
        {
            error = "one or both investigations were not found"
        });
    }

    var leftConfidence =
        left.Investigation.Hypotheses.OrderBy(item => item.Rank)
            .FirstOrDefault()?.Confidence;
    var rightConfidence =
        right.Investigation.Hypotheses.OrderBy(item => item.Rank)
            .FirstOrDefault()?.Confidence;

    return Results.Ok(new
    {
        left = new
        {
            id = left.Investigation.InvestigationId,
            left.Investigation.GeneratedAtUtc,
            left.Request.ServiceName,
            left.Investigation.Summary,
            evidenceCount = left.Investigation.Evidence.Count,
            primaryConfidence = leftConfidence,
            reasoningMode = left.Investigation.ReasoningTelemetry?.Mode
        },
        right = new
        {
            id = right.Investigation.InvestigationId,
            right.Investigation.GeneratedAtUtc,
            right.Request.ServiceName,
            right.Investigation.Summary,
            evidenceCount = right.Investigation.Evidence.Count,
            primaryConfidence = rightConfidence,
            reasoningMode = right.Investigation.ReasoningTelemetry?.Mode
        },
        delta = new
        {
            evidenceCount =
                right.Investigation.Evidence.Count -
                left.Investigation.Evidence.Count,
            primaryConfidence = leftConfidence.HasValue &&
                                rightConfidence.HasValue
                ? rightConfidence.Value - leftConfidence.Value
                : (double?)null
        }
    });
});

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}
