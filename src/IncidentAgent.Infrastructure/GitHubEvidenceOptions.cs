namespace IncidentAgent.Infrastructure;

public sealed class GitHubEvidenceOptions
{
    public const string SectionName = "Evidence:GitHub";

    public bool Enabled { get; init; }

    public string Owner { get; init; } = string.Empty;

    public string Repository { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = "https://api.github.com/";

    public int WindowMinutes { get; init; } = 30;

    public int MaxCommits { get; init; } = 20;
}
