using System.Globalization;
using System.Text.Json;
using IncidentAgent.Core;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Infrastructure;

public sealed class TempoTraceEvidenceSource : IIncidentEvidenceSource
{
    private readonly HttpClient _httpClient;
    private readonly TempoEvidenceOptions _options;

    public TempoTraceEvidenceSource(HttpClient httpClient, IOptions<TempoEvidenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        ObservabilityHttpConfiguration.Configure(
            _httpClient,
            _options.BaseUrl,
            _options.BearerToken,
            _options.TenantId);
    }

    public string Name => "tempo";

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
        var start = incident.StartedAtUtc.AddMinutes(-window).ToUnixTimeSeconds();
        var end = incident.StartedAtUtc.AddMinutes(window).ToUnixTimeSeconds();
        var query = _options.TraceQlTemplate.Replace(
            "{service}",
            ObservabilityHttpConfiguration.EscapeQuotedValue(service),
            StringComparison.Ordinal);

        var uri =
            "api/search" +
            $"?q={Uri.EscapeDataString(query)}" +
            $"&start={start}" +
            $"&end={end}" +
            $"&limit={Math.Clamp(_options.Limit, 1, 1000)}";

        var json = await HttpRequestExecutor.GetStringAsync(
            _httpClient,
            uri,
            _options.TimeoutSeconds,
            _options.RetryCount,
            cancellationToken);

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("traces", out var traces) ||
            traces.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<IncidentEvidence>();
        }

        var evidence = new List<IncidentEvidence>();

        foreach (var trace in traces.EnumerateArray())
        {
            var traceId = GetString(trace, "traceID");
            if (string.IsNullOrWhiteSpace(traceId))
            {
                continue;
            }

            var rootService = GetString(trace, "rootServiceName");
            var rootTraceName = GetString(trace, "rootTraceName");
            var durationMs = GetDouble(trace, "durationMs");
            var startNano = GetString(trace, "startTimeUnixNano");
            var timestamp = ParseUnixNanoseconds(startNano) ?? incident.StartedAtUtc;

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["trace.id"] = traceId,
                ["service.name"] = rootService ?? service,
                ["duration.ms"] = durationMs.ToString("0.###", CultureInfo.InvariantCulture)
            };

            MergeSpanAttributes(trace, attributes);

            evidence.Add(new IncidentEvidence(
                $"tempo-{traceId}",
                EvidenceType.Trace,
                Name,
                timestamp,
                rootService ?? service,
                $"{rootTraceName ?? "Trace"} took {durationMs:0.##} ms",
                BuildDetails(traceId, rootService, rootTraceName, durationMs),
                attributes));
        }

        return evidence;
    }

    private static void MergeSpanAttributes(
        JsonElement trace,
        IDictionary<string, string> destination)
    {
        if (!trace.TryGetProperty("spanSet", out var spanSet) ||
            !spanSet.TryGetProperty("spans", out var spans) ||
            spans.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var span in spans.EnumerateArray())
        {
            if (!span.TryGetProperty("attributes", out var attributes) ||
                attributes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var attribute in attributes.EnumerateArray())
            {
                var key = GetString(attribute, "key");
                if (string.IsNullOrWhiteSpace(key) ||
                    !attribute.TryGetProperty("value", out var value))
                {
                    continue;
                }

                var normalized = ExtractAttributeValue(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    destination[key] = normalized;
                }
            }
        }
    }

    private static string? ExtractAttributeValue(JsonElement value)
    {
        foreach (var name in new[]
                 {
                     "stringValue", "intValue", "doubleValue", "boolValue"
                 })
        {
            if (value.TryGetProperty(name, out var found))
            {
                return found.ToString();
            }
        }

        return null;
    }

    private static string BuildDetails(
        string traceId,
        string? service,
        string? operation,
        double durationMs) =>
        $"Trace {traceId}; service={service ?? "unknown"}; operation={operation ?? "unknown"}; duration={durationMs:0.##} ms.";

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) &&
        property.ValueKind != JsonValueKind.Null
            ? property.ToString()
            : null;

    private static double GetDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numeric))
        {
            return numeric;
        }

        return double.TryParse(
            property.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
    }

    private static DateTimeOffset? ParseUnixNanoseconds(string? value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nanos))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(nanos / 1_000_000L);
    }
}
