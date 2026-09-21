namespace IncidentAgent.Infrastructure;

public sealed class LokiEvidenceOptions
{
    public const string SectionName = "Evidence:Loki";
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "http://localhost:3100/";
    public string QueryTemplate { get; init; } = "{service_name=\"{service}\"}";
    public string BearerToken { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public int WindowMinutes { get; init; } = 15;
    public int Limit { get; init; } = 100;
    public int TimeoutSeconds { get; init; } = 10;
    public int RetryCount { get; init; } = 2;
}

public sealed class TempoEvidenceOptions
{
    public const string SectionName = "Evidence:Tempo";
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "http://localhost:3200/";
    public string TraceQlTemplate { get; init; } = "{ resource.service.name = \"{service}\" }";
    public string BearerToken { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public int WindowMinutes { get; init; } = 15;
    public int Limit { get; init; } = 20;
    public int TimeoutSeconds { get; init; } = 10;
    public int RetryCount { get; init; } = 2;
}

public sealed class PrometheusEvidenceOptions
{
    public const string SectionName = "Evidence:Prometheus";
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "http://localhost:9090/";
    public string QueryTemplate { get; init; } =
        "histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{service_name=\"{service}\"}[5m]))) * 1000";
    public string MetricName { get; init; } = "http.server.duration.p95";
    public string Unit { get; init; } = "ms";
    public string BearerToken { get; init; } = string.Empty;
    public int WindowMinutes { get; init; } = 15;
    public int StepSeconds { get; init; } = 30;
    public int TimeoutSeconds { get; init; } = 10;
    public int RetryCount { get; init; } = 2;
}
