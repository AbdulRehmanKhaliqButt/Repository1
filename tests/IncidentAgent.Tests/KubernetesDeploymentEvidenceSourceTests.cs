using System.Net;
using System.Text;
using IncidentAgent.Core;
using IncidentAgent.Infrastructure;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Tests;

public sealed class KubernetesDeploymentEvidenceSourceTests
{
    [Fact]
    public async Task CollectAsync_MapsHealthyDeploymentReplicaSetAndPods()
    {
        var handler = new SequenceHandler(
            JsonResponse("""
            {
              "items": [
                {
                  "metadata": {
                    "name": "payment-service",
                    "namespace": "production",
                    "generation": 42,
                    "labels": { "app": "payment-service" },
                    "creationTimestamp": "2026-09-19T15:40:00Z"
                  },
                  "spec": {
                    "replicas": 3,
                    "template": {
                      "spec": {
                        "containers": [
                          { "name": "api", "image": "ghcr.io/acme/payment-service:2026.09.19.4" }
                        ]
                      }
                    }
                  },
                  "status": {
                    "updatedReplicas": 3,
                    "availableReplicas": 3,
                    "unavailableReplicas": 0,
                    "conditions": [
                      {
                        "type": "Progressing",
                        "status": "True",
                        "lastUpdateTime": "2026-09-19T15:55:00Z"
                      }
                    ]
                  }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "metadata": {
                    "name": "payment-service-7f6c9b",
                    "labels": { "app": "payment-service" },
                    "ownerReferences": [
                      { "kind": "Deployment", "name": "payment-service" }
                    ]
                  }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "metadata": {
                    "name": "payment-service-7f6c9b-abcde",
                    "labels": { "app": "payment-service" },
                    "ownerReferences": [
                      { "kind": "ReplicaSet", "name": "payment-service-7f6c9b" }
                    ]
                  },
                  "status": {
                    "phase": "Running",
                    "containerStatuses": [
                      { "restartCount": 0, "state": { "running": {} } }
                    ]
                  }
                }
              ]
            }
            """));

        var source = CreateSource(handler);

        var evidence = await source.CollectAsync(Incident());

        var item = Assert.Single(evidence);
        Assert.Equal(EvidenceType.Deployment, item.Type);
        Assert.Equal("kubernetes", item.Source);
        Assert.Equal("2026.09.19.4", item.Attributes["deployment.version"]);
        Assert.Equal("3", item.Attributes["k8s.replicas.available"]);
        Assert.Equal("0", item.Attributes["k8s.failed_pods"]);
        Assert.Equal("false", item.Attributes["deployment.failed"]);
        Assert.Contains("healthy", item.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
            Assert.Equal("Bearer cluster-token", request.Headers.Authorization?.ToString()));
    }

    [Fact]
    public async Task CollectAsync_ReportsFailedRolloutAndUnhealthyPods()
    {
        var handler = new SequenceHandler(
            JsonResponse("""
            {
              "items": [
                {
                  "metadata": {
                    "name": "payment-service",
                    "generation": 43,
                    "labels": { "app": "payment-service" }
                  },
                  "spec": {
                    "replicas": 3,
                    "template": {
                      "spec": {
                        "containers": [
                          { "name": "api", "image": "ghcr.io/acme/payment-service:bad-release" }
                        ]
                      }
                    }
                  },
                  "status": {
                    "updatedReplicas": 2,
                    "availableReplicas": 1,
                    "unavailableReplicas": 2,
                    "conditions": [
                      {
                        "type": "Progressing",
                        "status": "False",
                        "lastUpdateTime": "2026-09-19T16:02:00Z"
                      }
                    ]
                  }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "metadata": {
                    "name": "payment-service-deadbeef",
                    "labels": { "app": "payment-service" },
                    "ownerReferences": [
                      { "kind": "Deployment", "name": "payment-service" }
                    ]
                  }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "metadata": {
                    "name": "payment-service-deadbeef-broken",
                    "labels": { "app": "payment-service" },
                    "ownerReferences": [
                      { "kind": "ReplicaSet", "name": "payment-service-deadbeef" }
                    ]
                  },
                  "status": {
                    "phase": "Running",
                    "containerStatuses": [
                      {
                        "restartCount": 3,
                        "state": { "waiting": { "reason": "CrashLoopBackOff" } }
                      }
                    ]
                  }
                }
              ]
            }
            """));

        var source = CreateSource(handler);

        var evidence = await source.CollectAsync(Incident());

        var item = Assert.Single(evidence);
        Assert.Equal("true", item.Attributes["deployment.failed"]);
        Assert.Equal("2", item.Attributes["k8s.replicas.unavailable"]);
        Assert.Equal("1", item.Attributes["k8s.failed_pods"]);
        Assert.Contains("unhealthy", item.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectAsync_WhenDisabled_DoesNotCallCluster()
    {
        var handler = new SequenceHandler();
        var source = new KubernetesDeploymentEvidenceSource(
            new HttpClient(handler),
            Options.Create(new KubernetesEvidenceOptions
            {
                Enabled = false,
                BaseUrl = "https://cluster.example/"
            }));

        var result = await source.CollectAsync(Incident());

        Assert.Empty(result);
        Assert.Empty(handler.Requests);
    }

    private static KubernetesDeploymentEvidenceSource CreateSource(
        SequenceHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new KubernetesEvidenceOptions
            {
                Enabled = true,
                BaseUrl = "https://cluster.example",
                Namespace = "production",
                BearerToken = "cluster-token",
                TokenFile = "",
                WindowMinutes = 30,
                RetryCount = 0
            }));

    private static IncidentRequest Incident() =>
        new(
            "Checkout failures",
            "Checkout failures started after deployment.",
            "payment-service",
            DateTimeOffset.Parse("2026-09-19T16:00:00Z"));

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class SequenceHandler(
        params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(Clone(request));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No response configured.");
            }

            return Task.FromResult(_responses.Dequeue());
        }

        private static HttpRequestMessage Clone(HttpRequestMessage source)
        {
            var clone = new HttpRequestMessage(source.Method, source.RequestUri);
            foreach (var header in source.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
