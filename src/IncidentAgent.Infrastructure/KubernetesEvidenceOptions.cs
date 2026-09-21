namespace IncidentAgent.Infrastructure;

public sealed class KubernetesEvidenceOptions
{
    public const string SectionName = "Evidence:Kubernetes";

    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = "https://kubernetes.default.svc/";

    public string Namespace { get; init; } = "default";

    public string BearerToken { get; init; } = string.Empty;

    public string TokenFile { get; init; } =
        "/var/run/secrets/kubernetes.io/serviceaccount/token";

    public int WindowMinutes { get; init; } = 30;

    public int TimeoutSeconds { get; init; } = 10;

    public int RetryCount { get; init; } = 2;
}
