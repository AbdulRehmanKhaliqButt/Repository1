using System.Net.Http.Headers;
using System.Text.Json;
using IncidentAgent.Core;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Infrastructure;

public sealed class KubernetesDeploymentEvidenceSource : IIncidentEvidenceSource
{
    private readonly HttpClient _httpClient;
    private readonly KubernetesEvidenceOptions _options;

    public KubernetesDeploymentEvidenceSource(
        HttpClient httpClient,
        IOptions<KubernetesEvidenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        var baseUrl = _options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? _options.BaseUrl
            : _options.BaseUrl + "/";

        _httpClient.BaseAddress ??= new Uri(baseUrl, UriKind.Absolute);

        var token = ResolveToken(_options);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public string Name => "kubernetes";

    public async Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Array.Empty<IncidentEvidence>();
        }

        var service = incident.ServiceName ?? "unknown";
        var ns = Uri.EscapeDataString(_options.Namespace);

        var deploymentJsonTask = GetAsync(
            $"apis/apps/v1/namespaces/{ns}/deployments",
            cancellationToken);
        var replicaSetJsonTask = GetAsync(
            $"apis/apps/v1/namespaces/{ns}/replicasets",
            cancellationToken);
        var podJsonTask = GetAsync(
            $"api/v1/namespaces/{ns}/pods",
            cancellationToken);

        await Task.WhenAll(deploymentJsonTask, replicaSetJsonTask, podJsonTask);

        using var deploymentsDoc = JsonDocument.Parse(await deploymentJsonTask);
        using var replicaSetsDoc = JsonDocument.Parse(await replicaSetJsonTask);
        using var podsDoc = JsonDocument.Parse(await podJsonTask);

        var replicaSets = ReadItems(replicaSetsDoc.RootElement)
            .Where(item => MatchesService(item, service))
            .ToArray();
        var pods = ReadItems(podsDoc.RootElement)
            .Where(item => MatchesService(item, service))
            .ToArray();

        var windowMinutes = Math.Clamp(_options.WindowMinutes, 1, 1440);
        var windowStart = incident.StartedAtUtc.AddMinutes(-windowMinutes);
        var windowEnd = incident.StartedAtUtc.AddMinutes(windowMinutes);

        var evidence = new List<IncidentEvidence>();

        foreach (var deployment in ReadItems(deploymentsDoc.RootElement)
                     .Where(item => MatchesService(item, service)))
        {
            var rolloutTime =
                GetConditionTime(deployment, "Progressing") ??
                GetTimestamp(deployment, "metadata", "creationTimestamp") ??
                incident.StartedAtUtc;

            if (rolloutTime < windowStart || rolloutTime > windowEnd)
            {
                continue;
            }

            var name = GetString(deployment, "metadata", "name") ?? service;
            var generation = GetLong(deployment, "metadata", "generation");
            var desired = GetLong(deployment, "spec", "replicas");
            var available = GetLong(deployment, "status", "availableReplicas");
            var updated = GetLong(deployment, "status", "updatedReplicas");
            var unavailable = GetLong(deployment, "status", "unavailableReplicas");
            var images = GetContainerImages(deployment);
            var failed = HasFailedCondition(deployment) || unavailable > 0;

            var relatedReplicaSets = replicaSets
                .Where(rs => IsOwnedBy(rs, "Deployment", name))
                .ToArray();

            var relatedPods = pods
                .Where(pod => relatedReplicaSets.Any(rs =>
                {
                    var rsName = GetString(rs, "metadata", "name");
                    return !string.IsNullOrWhiteSpace(rsName) &&
                           IsOwnedBy(pod, "ReplicaSet", rsName);
                }))
                .ToArray();

            var failedPods = relatedPods.Count(IsFailedPod);

            var attributes = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["k8s.namespace"] = _options.Namespace,
                ["k8s.deployment.name"] = name,
                ["k8s.deployment.generation"] = generation.ToString(),
                ["k8s.replicas.desired"] = desired.ToString(),
                ["k8s.replicas.updated"] = updated.ToString(),
                ["k8s.replicas.available"] = available.ToString(),
                ["k8s.replicas.unavailable"] = unavailable.ToString(),
                ["k8s.replicasets"] = relatedReplicaSets.Length.ToString(),
                ["k8s.pods"] = relatedPods.Length.ToString(),
                ["k8s.failed_pods"] = failedPods.ToString(),
                ["deployment.failed"] = failed.ToString().ToLowerInvariant()
            };

            if (images.Count > 0)
            {
                attributes["container.images"] = string.Join(",", images);
                attributes["deployment.version"] = InferVersion(images[0]);
            }

            var health = failed || failedPods > 0
                ? "rollout has unhealthy replicas or pods"
                : "rollout is healthy";

            evidence.Add(new IncidentEvidence(
                $"k8s-deployment-{_options.Namespace}-{name}-{generation}",
                EvidenceType.Deployment,
                Name,
                rolloutTime,
                service,
                $"Kubernetes deployment {name} generation {generation}: {health}",
                $"Namespace={_options.Namespace}; images={string.Join(", ", images)}; desired={desired}; updated={updated}; available={available}; unavailable={unavailable}; failedPods={failedPods}.",
                attributes));
        }

        return evidence;
    }

    private Task<string> GetAsync(
        string uri,
        CancellationToken cancellationToken) =>
        HttpRequestExecutor.GetStringAsync(
            _httpClient,
            uri,
            _options.TimeoutSeconds,
            _options.RetryCount,
            cancellationToken);

    private static string ResolveToken(KubernetesEvidenceOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BearerToken))
        {
            return options.BearerToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.TokenFile) &&
            File.Exists(options.TokenFile))
        {
            return File.ReadAllText(options.TokenFile).Trim();
        }

        return string.Empty;
    }

    private static JsonElement[] ReadItems(JsonElement root)
    {
        if (!root.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static bool MatchesService(JsonElement item, string service)
    {
        var name = GetString(item, "metadata", "name");
        if (!string.IsNullOrWhiteSpace(name) &&
            (name.Equals(service, StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith(service + "-", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!TryGet(item, out var labels, "metadata", "labels") ||
            labels.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var label in labels.EnumerateObject())
        {
            if (label.Value.ValueKind == JsonValueKind.String &&
                string.Equals(
                    label.Value.GetString(),
                    service,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOwnedBy(
        JsonElement item,
        string kind,
        string name)
    {
        if (!TryGet(item, out var ownerReferences, "metadata", "ownerReferences") ||
            ownerReferences.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var owner in ownerReferences.EnumerateArray())
        {
            var ownerKind = owner.TryGetProperty("kind", out var k)
                ? k.GetString()
                : null;
            var ownerName = owner.TryGetProperty("name", out var n)
                ? n.GetString()
                : null;

            if (string.Equals(ownerKind, kind, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ownerName, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFailedCondition(JsonElement deployment)
    {
        if (!TryGet(deployment, out var conditions, "status", "conditions") ||
            conditions.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var condition in conditions.EnumerateArray())
        {
            var type = condition.TryGetProperty("type", out var t)
                ? t.GetString()
                : null;
            var status = condition.TryGetProperty("status", out var s)
                ? s.GetString()
                : null;

            if ((string.Equals(type, "Progressing", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "Available", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(status, "False", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFailedPod(JsonElement pod)
    {
        var phase = GetString(pod, "status", "phase");
        if (string.Equals(phase, "Failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(phase, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryGet(pod, out var statuses, "status", "containerStatuses") ||
            statuses.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return statuses.EnumerateArray().Any(status =>
            GetLong(status, "restartCount") > 0 ||
            TryGet(status, out _, "state", "waiting"));
    }

    private static DateTimeOffset? GetConditionTime(
        JsonElement deployment,
        string conditionType)
    {
        if (!TryGet(deployment, out var conditions, "status", "conditions") ||
            conditions.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var matches = conditions.EnumerateArray()
            .Where(condition =>
                string.Equals(
                    condition.TryGetProperty("type", out var type)
                        ? type.GetString()
                        : null,
                    conditionType,
                    StringComparison.OrdinalIgnoreCase))
            .Select(condition =>
            {
                var value = condition.TryGetProperty("lastUpdateTime", out var time)
                    ? time.GetString()
                    : null;
                return DateTimeOffset.TryParse(value, out var parsed)
                    ? parsed
                    : (DateTimeOffset?)null;
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return matches.Length == 0 ? null : matches.Max();
    }

    private static List<string> GetContainerImages(JsonElement deployment)
    {
        if (!TryGet(
                deployment,
                out var containers,
                "spec",
                "template",
                "spec",
                "containers") ||
            containers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return containers.EnumerateArray()
            .Select(container =>
                container.TryGetProperty("image", out var image)
                    ? image.GetString()
                    : null)
            .Where(image => !string.IsNullOrWhiteSpace(image))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string InferVersion(string image)
    {
        var digestIndex = image.IndexOf('@');
        if (digestIndex >= 0 && digestIndex < image.Length - 1)
        {
            return image[(digestIndex + 1)..];
        }

        var slash = image.LastIndexOf('/');
        var colon = image.LastIndexOf(':');

        return colon > slash && colon < image.Length - 1
            ? image[(colon + 1)..]
            : image;
    }

    private static string? GetString(
        JsonElement element,
        params string[] path)
    {
        return TryGet(element, out var value, path) &&
               value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static long GetLong(
        JsonElement element,
        params string[] path)
    {
        if (!TryGet(element, out var value, path))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var number))
        {
            return number;
        }

        return long.TryParse(value.ToString(), out var parsed)
            ? parsed
            : 0;
    }

    private static DateTimeOffset? GetTimestamp(
        JsonElement element,
        params string[] path)
    {
        var value = GetString(element, path);
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private static bool TryGet(
        JsonElement element,
        out JsonElement value,
        params string[] path)
    {
        value = element;

        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }
}
