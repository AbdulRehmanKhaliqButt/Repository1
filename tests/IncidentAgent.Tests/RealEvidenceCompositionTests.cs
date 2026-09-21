using System.Net;
using System.Text;
using IncidentAgent.Core;
using IncidentAgent.Infrastructure;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Tests;

public sealed class RealEvidenceCompositionTests
{
    [Fact]
    public async Task Investigator_CorrelatesRealProviderShapesAcrossGitHubLokiTempoAndPrometheus()
    {
        var incident = new IncidentRequest(
            "Checkout failures",
            "Checkout requests are timing out after a deployment.",
            "payment-service",
            DateTimeOffset.Parse("2026-09-19T16:00:00Z"));

        var github = new GitHubCommitEvidenceSource(
            Client("""
            [
              {
                "sha": "8f31e0abcdef1234",
                "html_url": "https://github.com/acme/payment-service/commit/8f31e0abcdef1234",
                "commit": {
                  "message": "tune payment database pool settings",
                  "author": { "date": "2026-09-19T15:41:00Z" }
                }
              }
            ]
            """),
            Options.Create(new GitHubEvidenceOptions
            {
                Enabled = true,
                Owner = "acme",
                Repository = "payment-service"
            }));

        var loki = new LokiLogEvidenceSource(
            Client("""
            {
              "status": "success",
              "data": {
                "resultType": "streams",
                "result": [
                  {
                    "stream": { "service_name": "payment-service", "level": "error" },
                    "values": [
                      ["1789833780000000000", "NpgsqlException: timeout while waiting for an available pooled connection"]
                    ]
                  }
                ]
              }
            }
            """),
            Options.Create(new LokiEvidenceOptions
            {
                Enabled = true,
                BaseUrl = "http://loki:3100/",
                RetryCount = 0
            }));

        var tempo = new TempoTraceEvidenceSource(
            Client("""
            {
              "traces": [
                {
                  "traceID": "trace-db-001",
                  "rootServiceName": "payment-service",
                  "rootTraceName": "POST /checkout",
                  "startTimeUnixNano": "1789833840000000000",
                  "durationMs": 2400,
                  "spanSet": {
                    "spans": [
                      {
                        "attributes": [
                          { "key": "db.system", "value": { "stringValue": "postgresql" } }
                        ]
                      }
                    ]
                  }
                }
              ]
            }
            """),
            Options.Create(new TempoEvidenceOptions
            {
                Enabled = true,
                BaseUrl = "http://tempo:3200/",
                RetryCount = 0
            }));

        var prometheus = new PrometheusMetricEvidenceSource(
            Client("""
            {
              "status": "success",
              "data": {
                "resultType": "matrix",
                "result": [
                  {
                    "metric": { "service_name": "payment-service" },
                    "values": [
                      [1789833720, "180"],
                      [1789833900, "2800"]
                    ]
                  }
                ]
              }
            }
            """),
            Options.Create(new PrometheusEvidenceOptions
            {
                Enabled = true,
                BaseUrl = "http://prometheus:9090/",
                MetricName = "http.server.duration.p95",
                Unit = "ms",
                RetryCount = 0
            }));

        var investigator = new IncidentInvestigator(
        [
            new DemoDeploymentEvidenceSource(),
            github,
            loki,
            tempo,
            prometheus
        ]);

        var result = await investigator.InvestigateAsync(incident);

        Assert.Contains(result.Evidence, item => item.Source == "github");
        Assert.Contains(result.Evidence, item => item.Source == "loki");
        Assert.Contains(result.Evidence, item => item.Source == "tempo");
        Assert.Contains(result.Evidence, item => item.Source == "prometheus");
        Assert.DoesNotContain(result.Evidence, item => item.Type == EvidenceType.SourceError);
        Assert.True(result.Hypotheses[0].Title.Contains(
            "Database connection pool",
            StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Hypotheses[0].Confidence >= 0.90);
        Assert.Equal(5, result.Hypotheses[0].EvidenceIds.Count);
    }

    private static HttpClient Client(string json) =>
        new(new StaticJsonHandler(json));

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
