using IncidentAgent.Core;

namespace IncidentAgent.Infrastructure;

public sealed class DemoDeploymentEvidenceSource : IIncidentEvidenceSource
{
    public string Name => "demo-kubernetes";

    public Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        var service = incident.ServiceName ?? "payment-service";
        IReadOnlyCollection<IncidentEvidence> result =
        [
            new(
                "deploy-001",
                EvidenceType.Deployment,
                Name,
                incident.StartedAtUtc.AddMinutes(-8),
                service,
                "Deployment payment-service 2026.09.19.4 completed",
                "ReplicaSet payment-service-7f6c9b became ready after image rollout.",
                new Dictionary<string, string>
                {
                    ["deployment.version"] = "2026.09.19.4",
                    ["k8s.namespace"] = "production",
                    ["replicas"] = "6"
                })
        ];

        return Task.FromResult(result);
    }
}

public sealed class DemoCommitEvidenceSource : IIncidentEvidenceSource
{
    public string Name => "demo-github";

    public Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        var service = incident.ServiceName ?? "payment-service";
        IReadOnlyCollection<IncidentEvidence> result =
        [
            new(
                "commit-001",
                EvidenceType.Commit,
                Name,
                incident.StartedAtUtc.AddMinutes(-19),
                service,
                "8f31e0a tune payment database pool settings",
                "Changed payment database connection-pool configuration and retry behavior.",
                new Dictionary<string, string>
                {
                    ["git.sha"] = "8f31e0a",
                    ["git.branch"] = "main",
                    ["git.repository"] = "commerce/payment-service"
                })
        ];

        return Task.FromResult(result);
    }
}

public sealed class DemoMetricEvidenceSource : IIncidentEvidenceSource
{
    public string Name => "demo-prometheus";

    public Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        var service = incident.ServiceName ?? "payment-service";
        IReadOnlyCollection<IncidentEvidence> result =
        [
            new(
                "metric-001",
                EvidenceType.Metric,
                Name,
                incident.StartedAtUtc.AddMinutes(2),
                service,
                "HTTP p95 latency increased from 180 ms to 2.8 s",
                "The latency increase begins within ten minutes of the latest deployment.",
                new Dictionary<string, string>
                {
                    ["metric.name"] = "http.server.duration.p95",
                    ["baseline.ms"] = "180",
                    ["observed.ms"] = "2800"
                })
        ];

        return Task.FromResult(result);
    }
}

public sealed class DemoLogEvidenceSource : IIncidentEvidenceSource
{
    public string Name => "demo-loki";

    public Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        var service = incident.ServiceName ?? "payment-service";
        IReadOnlyCollection<IncidentEvidence> result =
        [
            new(
                "log-001",
                EvidenceType.Log,
                Name,
                incident.StartedAtUtc.AddMinutes(3),
                service,
                "Database command timeout while authorizing payment",
                "NpgsqlException: timeout while waiting for an available pooled connection.",
                new Dictionary<string, string>
                {
                    ["level"] = "Error",
                    ["exception.type"] = "NpgsqlException",
                    ["operation"] = "AuthorizePayment"
                })
        ];

        return Task.FromResult(result);
    }
}

public sealed class DemoTraceEvidenceSource : IIncidentEvidenceSource
{
    public string Name => "demo-tempo";

    public Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        var service = incident.ServiceName ?? "payment-service";
        IReadOnlyCollection<IncidentEvidence> result =
        [
            new(
                "trace-001",
                EvidenceType.Trace,
                Name,
                incident.StartedAtUtc.AddMinutes(4),
                service,
                "Checkout trace spends 2.4 s waiting on PostgreSQL",
                "Trace checkout -> payment-service -> PostgreSQL; the database span dominates total request duration.",
                new Dictionary<string, string>
                {
                    ["trace.id"] = "4fd74c4b711f4a7a",
                    ["span.name"] = "SELECT payment_method",
                    ["db.system"] = "postgresql",
                    ["duration.ms"] = "2400"
                })
        ];

        return Task.FromResult(result);
    }
}
