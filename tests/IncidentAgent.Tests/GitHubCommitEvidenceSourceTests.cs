using System.Net;
using System.Text;
using IncidentAgent.Core;
using IncidentAgent.Infrastructure;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Tests;

public sealed class GitHubCommitEvidenceSourceTests
{
    [Fact]
    public async Task CollectAsync_MapsCommitsFromIncidentWindowIntoEvidence()
    {
        const string responseJson = """
        [
          {
            "sha": "abcdef1234567890",
            "html_url": "https://github.com/acme/payment-service/commit/abcdef1234567890",
            "commit": {
              "message": "fix: tune payment database pool\nReduce idle lifetime.",
              "author": {
                "date": "2026-09-19T15:55:00Z"
              }
            }
          }
        ]
        """;

        var handler = new StubHttpMessageHandler(responseJson);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GitHubEvidenceOptions
        {
            Enabled = true,
            Owner = "acme",
            Repository = "payment-service",
            ApiBaseUrl = "https://api.github.com/",
            WindowMinutes = 30,
            MaxCommits = 20
        });

        var source = new GitHubCommitEvidenceSource(httpClient, options);
        var incident = new IncidentRequest(
            "Checkout failures",
            "Checkout is timing out.",
            "payment-service",
            DateTimeOffset.Parse("2026-09-19T16:00:00Z"));

        var evidence = await source.CollectAsync(incident);

        var item = Assert.Single(evidence);
        Assert.Equal(EvidenceType.Commit, item.Type);
        Assert.Equal("github", item.Source);
        Assert.Contains("abcdef1", item.Summary);
        Assert.Equal("acme/payment-service", item.Attributes["git.repository"]);
        Assert.Contains("/commits?", handler.LastRequestUri?.ToString());
        Assert.Contains("since=", handler.LastRequestUri?.ToString());
        Assert.Contains("until=", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task CollectAsync_WhenDisabled_DoesNotCallGitHub()
    {
        var handler = new StubHttpMessageHandler("[]");
        var source = new GitHubCommitEvidenceSource(
            new HttpClient(handler),
            Options.Create(new GitHubEvidenceOptions
            {
                Enabled = false,
                Owner = "acme",
                Repository = "payment-service"
            }));

        var evidence = await source.CollectAsync(new IncidentRequest(
            "Incident",
            "Description",
            "payment-service",
            DateTimeOffset.UtcNow));

        Assert.Empty(evidence);
        Assert.Null(handler.LastRequestUri);
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseJson,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
