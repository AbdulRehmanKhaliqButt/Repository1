using System.Text.Json.Serialization;
using IncidentAgent.Core;
using IncidentAgent.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddIncidentInvestigationDemo();

var app = builder.Build();

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
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "title is required" });
    }

    var normalized = request.StartedAtUtc == default
        ? request with { StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10) }
        : request;

    var investigation = await investigator.InvestigateAsync(normalized, cancellationToken);
    return Results.Ok(investigation);
});

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}
