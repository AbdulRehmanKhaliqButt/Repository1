using System.Net;
using System.Text;
using IncidentAgent.Core;
using IncidentAgent.Infrastructure;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Tests;

public sealed class ObservabilityEvidenceSourceTests
{
    [Fact]
    public async Task Loki_MapsStreamEntriesIntoLogEvidence()
    {
        const string json = """
        {
          "status": "success",
          "data": {
            "resultType": "streams",
            "result": [
              {
                "stream": {
                  "service_name": "payment-service",
                  "level": "error"
                },
                "values": [
                  ["1789833603000000000", "NpgsqlException: timeout while waiting for a pooled connection"]
                ]
              }
            ]
          }
        }
        """;

        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(json)
            });

        var source = new LokiLogEvidenceSource(
            new HttpClient(handler),
            Options.Create(new LokiEvidenceOptions
            {
                Enabled = true,
                BaseUrl = "http://loki:3100/",
                QueryTemplate = "{service_name=\"{service}\"}",
                BearerToken = "secret-token",
                TenantId = "tenant-a",
                RetryCount = 0
            }));

        var evidence = await source.CollectAsync(Incident());

        var item = Assert.Single(evidence);
        Assert.Equal(EvidenceType.Log, item.Type);
        Assert.Equal("loki", item.Source);
        Assert.True(item.Summary.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("error", item.Attributes["level"]);
        Assert.DoesNotContain("secret-token", item.Details);
        Assert.Contains("/loki/api/v1/query_range?", handler.Requests.Single().RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Requests.Single().Headers.Authorization?.Scheme);
        Assert.Equal("secret-token", handler.Requests.Single().Headers.Authorization?.Parameter);
        Assert.True(handler.Requests.Single().Headers.TryGetValues("X-Scope-OrgID", out var tenantValues));
        Assert.Equal("tenant-a", Assert.Single(tenantValues));
    }

    [Fact]
    public async Task Tempo_MapsTraceAndSpanAttributesIntoTraceEvidence()
    {
        const string json = """
        {
          "traces": [
            {
              "traceID": "5b8efff798038103d269b633813fc700",
              "rootServiceName": "payment-service",
              "rootTraceName": "POST /checkout",
              "startTimeUnixNano": "1789833604000000000",
              "durationMs": 2400,
              "spanSet": {
                "spans": [
                  {
                    "attributes": [
                      { "key": "db.system", "value": { "stringValue": "postgresql" } },
                      { "key": "db.operation.name", "value": { "stringValue": "SELECT" } }
                    ]
                  }
                ]
              }
            }
          ]
        }
        """;

        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(json)
            });

        var source = new TempoTraceEvidenceSource(
            new HttpClient(handler),
            Options.Create(new TempoEvidenceOptions
            {
                Enabled = true,
                BaseUrl = "http://tempo:3200/",
                TraceQlTemplate = "{ resource.service.name = \"{service}\" }",
                RetryCount = 0
            }));

        var evidence = await source.CollectAsync(Incident());

        var item = Assert.Single(evidence);
        Assert.Equal(EvidenceType.Trace, item.Type);
        Assert.Equal("tempo", item.Source);
        Assert.Equal("postgresql", item.Attributes["db.system"]);
        Assert.Equal("2400", item.Attributes["duration.ms"]);
        Assert.Equal("5b8efff798038103d269b633813fc700", item.Attributes["trace.id"]);
        Assert.Contains("/api/search?", handler.Requests.Single().RequestUri!.ToString());
        Assert.Contains("q=", handler.Requests.Single().RequestUri!.Query);
    }

    [Fact]
    public async Task Prometheus_SummarizesRangeSeriesIntoMetricEvidence()
    {
        const string json = """
        {
          "status": "success",
          "data": {
            "resultType": "matrix",
            "result": [
              {
                "metric": {
                  "service_name": "payment-service",
                  "instance": "payment-1"
                },
                "values": [
                  [1789833601, "180"],
                  [1789833631, "2800"],
                  [1789833661, "1900"]
                ]
              }
            ]
          }
        }
        """;

        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(json)
            });

        var source = new PrometheusMetricEvidenceSource(
            new HttpClient(handler),
            Options.Create(new PrometheusEvidenceOptions
            {
                Enabled = true,
                BaseUrl = "http://prometheus:9090/",
                QueryTemplate = "p95_latency_ms{service_name=\"{service}\"}",
                MetricName = "http.server.duration.p95",
                Unit = "ms",
                RetryCount = 0
            }));

        var evidence = await source.CollectAsync(Incident());

        var item = Assert.Single(evidence);
        Assert.Equal(EvidenceType.Metric, item.Type);
        Assert.Equal("prometheus", item.Source);
        Assert.Equal("2800", item.Attributes["observed.max"]);
        Assert.Equal("2800", item.Attributes["observed.ms"]);
        Assert.Equal("1900", item.Attributes["observed.latest"]);
        Assert.Equal("payment-1", item.Attributes["instance"]);
        Assert.Contains("/api/v1/query_range?", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task Loki_RetriesTransientFailure_ThenSucceeds()
    {
        const string successJson = """
        {
          "status": "success",
          "data": {
            "resultType": "streams",
            "result": []
          }
        }
        """;

        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(successJson)
            });

        var source = new LokiLogEvidenceSource(
            new HttpClient(handler),
            Options.Create(new LokiEvidenceOptions
            {
                Enabled = true,
                BaseUrl = "http://loki:3100/",
                RetryCount = 1,
                TimeoutSeconds = 2
            }));

        var evidence = await source.CollectAsync(Incident());

        Assert.Empty(evidence);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DisabledProviders_DoNotMakeHttpRequests()
    {
        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var loki = new LokiLogEvidenceSource(
            new HttpClient(handler),
            Options.Create(new LokiEvidenceOptions { Enabled = false }));

        var tempo = new TempoTraceEvidenceSource(
            new HttpClient(handler),
            Options.Create(new TempoEvidenceOptions { Enabled = false }));

        var prometheus = new PrometheusMetricEvidenceSource(
            new HttpClient(handler),
            Options.Create(new PrometheusEvidenceOptions { Enabled = false }));

        Assert.Empty(await loki.CollectAsync(Incident()));
        Assert.Empty(await tempo.CollectAsync(Incident()));
        Assert.Empty(await prometheus.CollectAsync(Incident()));
        Assert.Empty(handler.Requests);
    }

    private static IncidentRequest Incident() =>
        new(
            "Checkout failures",
            "Checkout requests are timing out.",
            "payment-service",
            DateTimeOffset.Parse("2026-09-19T16:00:00Z"));

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private sealed class SequenceHttpMessageHandler(
        params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No response configured.");
            }

            return Task.FromResult(_responses.Dequeue());
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage source)
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
