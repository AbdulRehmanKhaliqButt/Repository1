using System.Net.Http.Headers;

namespace IncidentAgent.Infrastructure;

internal static class ObservabilityHttpConfiguration
{
    public static void Configure(
        HttpClient httpClient,
        string baseUrl,
        string bearerToken,
        string tenantId = "")
    {
        var normalizedBaseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : baseUrl + "/";

        httpClient.BaseAddress ??= new Uri(normalizedBaseUrl, UriKind.Absolute);

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            httpClient.DefaultRequestHeaders.Remove("X-Scope-OrgID");
            httpClient.DefaultRequestHeaders.Add("X-Scope-OrgID", tenantId);
        }
    }

    public static string EscapeQuotedValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);
}
