using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IncidentAgent.Core;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Infrastructure;

public sealed class GitHubCommitEvidenceSource : IIncidentEvidenceSource
{
    private readonly HttpClient _httpClient;
    private readonly GitHubEvidenceOptions _options;

    public GitHubCommitEvidenceSource(
        HttpClient httpClient,
        IOptions<GitHubEvidenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress ??= new Uri(_options.ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("IncidentInvestigationAgent/0.1");
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.Token);
        }
    }

    public string Name => "github";

    public async Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled ||
            string.IsNullOrWhiteSpace(_options.Owner) ||
            string.IsNullOrWhiteSpace(_options.Repository))
        {
            return Array.Empty<IncidentEvidence>();
        }

        var windowMinutes = Math.Clamp(_options.WindowMinutes, 5, 240);
        var maxCommits = Math.Clamp(_options.MaxCommits, 1, 100);
        var since = incident.StartedAtUtc.AddMinutes(-windowMinutes);
        var until = incident.StartedAtUtc.AddMinutes(windowMinutes);

        var relativeUrl =
            $"repos/{Uri.EscapeDataString(_options.Owner)}/{Uri.EscapeDataString(_options.Repository)}/commits" +
            $"?since={Uri.EscapeDataString(since.UtcDateTime.ToString("O"))}" +
            $"&until={Uri.EscapeDataString(until.UtcDateTime.ToString("O"))}" +
            $"&per_page={maxCommits}";

        var commits = await _httpClient.GetFromJsonAsync<List<GitHubCommitDto>>(
            relativeUrl,
            cancellationToken);

        if (commits is null || commits.Count == 0)
        {
            return Array.Empty<IncidentEvidence>();
        }

        var service = incident.ServiceName ?? _options.Repository;

        return commits
            .Where(commit => !string.IsNullOrWhiteSpace(commit.Sha))
            .Select(commit =>
            {
                var message = commit.Commit?.Message ?? "Commit in incident window";
                var firstLine = message
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? "Commit in incident window";

                var shortSha = commit.Sha.Length > 7
                    ? commit.Sha[..7]
                    : commit.Sha;

                return new IncidentEvidence(
                    $"github-commit-{shortSha}",
                    EvidenceType.Commit,
                    Name,
                    commit.Commit?.Author?.Date ?? incident.StartedAtUtc,
                    service,
                    $"{shortSha} {firstLine}",
                    message,
                    new Dictionary<string, string>
                    {
                        ["git.sha"] = commit.Sha,
                        ["git.repository"] = $"{_options.Owner}/{_options.Repository}",
                        ["git.url"] = commit.HtmlUrl ?? string.Empty
                    });
            })
            .OrderBy(item => item.TimestampUtc)
            .ToArray();
    }

    private sealed record GitHubCommitDto(
        [property: JsonPropertyName("sha")] string Sha,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("commit")] GitHubCommitDetailsDto? Commit);

    private sealed record GitHubCommitDetailsDto(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("author")] GitHubCommitAuthorDto? Author);

    private sealed record GitHubCommitAuthorDto(
        [property: JsonPropertyName("date")] DateTimeOffset Date);
}
