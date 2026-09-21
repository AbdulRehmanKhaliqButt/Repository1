using System.Globalization;
using System.Text.Json;
using IncidentAgent.Core;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Infrastructure;

public sealed class LokiLogEvidenceSource : IIncidentEvidenceSource
{
    private readonly HttpClient _httpClient;
    private readonly LokiEvidenceOptions _options;

    public LokiLogEvidenceSource(HttpClient httpClient, IOptions<LokiEvidenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        ObservabilityHttpConfiguration.Configure(
            _httpClient,
            _options.BaseUrl,
            _options.BearerToken,
            _options.TenantId);
    }

    public string Name => "loki";

    public async Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Array.Empty<IncidentEvidence>();
        }

        var service = incident.ServiceName ?? "unknown";
        var window = Math.Clamp(_options.WindowMinutes, 1, 240);
        var start = incident.StartedAtUtc.AddMinutes(-window);
        var end = incident.StartedAtUtc.AddMinutes(window);
        var query = _options.QueryTemplate.Replace(
            "{service}",
            ObservabilityHttpConfiguration.EscapeQuotedValue(service),
            StringComparison.Ordinal);

        var uri =
            "loki/api/v1/query_range" +
            $"?query={Uri.EscapeDataString(query)}" +
            $"&start={ToUnixNanoseconds(start)}" +
            $"&end={ToUnixNanoseconds(end)}" +
            $"&limit={Math.Clamp(_options.Limit, 1, 5000)}" +
            "&direction=forward";

        var json = await HttpRequestExecutor.GetStringAsync(
            _httpClient,
            uri,
            _options.TimeoutSeconds,
            _options.RetryCount,
            cancellationToken);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("status", out var status) ||
            !string.Equals(status.GetString(), "success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Loki returned a non-success response.");
        }

        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("result", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<IncidentEvidence>();
        }

        var evidence = new List<IncidentEvidence>();

        foreach (var streamResult in results.EnumerateArray())
        {
            var labels = ReadStringObject(streamResult, "stream");

            if (!streamResult.TryGetProperty("values", out var values) ||
                values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in values.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 2)
                {
                    continue;
                }

                var timestampText = item[0].GetString();
                var line = item[1].GetString() ?? string.Empty;

                if (!TryParseUnixNanoseconds(timestampText, out var timestamp))
                {
                    timestamp = incident.StartedAtUtc;
                }

                var attributes = new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase)
                {
                    ["log.timestamp.unix_nano"] = timestampText ?? string.Empty
                };

                evidence.Add(new IncidentEvidence(
                    $"loki-{timestamp.ToUnixTimeMilliseconds()}-{evidence.Count}",
                    EvidenceType.Log,
                    Name,
                    timestamp,
                    service,
                    Summarize(line),
                    line,
                    attributes));
            }
        }

        return evidence;
    }

    private static Dictionary<string, string> ReadStringObject(JsonElement parent, string property)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!parent.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var item in value.EnumerateObject())
        {
            result[item.Name] = item.Value.ToString();
        }

        return result;
    }

    private static string Summarize(string value)
    {
        var singleLine = value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return singleLine.Length <= 180
            ? singleLine
            : singleLine[..177] + "...";
    }

    private static string ToUnixNanoseconds(DateTimeOffset value)
    {
        var milliseconds = value.ToUnixTimeMilliseconds();
        return (milliseconds * 1_000_000L).ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParseUnixNanoseconds(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nanoseconds))
        {
            return false;
        }

        timestamp = DateTimeOffset.FromUnixTimeMilliseconds(nanoseconds / 1_000_000L);
        return true;
    }
}
